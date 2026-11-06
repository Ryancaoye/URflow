using System;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace URflow
{
    // Navigation belongs to the host; animation data belongs to the tool service.
    internal sealed class URflowToolsHost : IDisposable
    {
        private const float PanelCloseSeconds = 0.28f;
        private sealed class ToolEntry
        {
            public readonly string English;
            public readonly string Chinese;
            public readonly string Icon;
            public readonly Action Open;

            public ToolEntry(string english, string chinese, string icon, Action open)
            { English = english; Chinese = chinese; Icon = icon; Open = open; }
        }

        private readonly AnimBool _open = new AnimBool(false);
        private readonly URflowKeyframeMovePanel _panel;
        private readonly Action _repaint;
        private readonly ToolEntry[] _tools;
        private string _panelError;
        private float _progress;
        private float _transitionFrom;
        private bool _transitionTarget;
        private bool _transitioning;
        private double _transitionStarted;
        private double _sweepStarted = -100d;
        private float _toolbarScroll;
        private float _dragScrollStart;
        private Vector2 _dragOrigin;
        private bool _trackingDrag;
        private bool _dragging;
        private bool _disposed;

        internal Rect ToolbarViewport { get; private set; }
        internal Rect FirstToolRect { get; private set; }
        internal Rect PanelRect { get; private set; }
        internal float PanelProgress { get { return _progress; } }
        internal float PanelOpacity { get { return _progress; } }
        internal Rect PanelActionsRect { get { return OffsetPanelRect(_panel.ActionsRect); } }
        internal Rect PanelCloseRect { get { return OffsetPanelRect(_panel.CloseRect); } }
        internal Rect PanelBodyRect { get { return OffsetPanelRect(_panel.BodyRect); } }

        private Rect OffsetPanelRect(Rect local)
        {
            local.position += PanelRect.position;
            return local;
        }

        public bool IsOpen
        {
            get { SynchronizeExternalState(); return _open.target || _progress > 0f; }
        }

        public URflowToolsHost(Action repaint)
        {
            _repaint = repaint;
            _panel = new URflowKeyframeMovePanel(repaint, StartSweep);
            _tools = new[] { new ToolEntry("Keyframe Move", "关键帧移动", "MoveTool", Open) };
            // Navigation is immediate; the independent timestamp owns the 400ms visual transition.
            _open.valueChanged.AddListener(Repaint);
            Undo.undoRedoPerformed += RefreshIfOpen;
            EditorApplication.update += Tick;
        }

        private void Repaint() { if (!_disposed) _repaint(); }

        public void RefreshIfOpen()
        {
            if (_open.target) _panel.Refresh();
        }

        public void DrawToolbar(GUIStyle button, Func<string, string, string> text)
        {
            Rect strip = GUILayoutUtility.GetRect(0f, 84f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(strip, URflowVisuals.PanelBackground);
            EditorGUI.DrawRect(new Rect(strip.x, strip.y, strip.width, 1f), URflowVisuals.Divider);
            GUIStyle title = new GUIStyle(EditorStyles.label) { fontSize = 10, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(.55f, .56f, .59f);
            GUI.Label(new Rect(strip.x + 16f, strip.y + 7f, strip.width - 32f, 16f), text("TOOLS", "工具"), title);

            ToolbarViewport = new Rect(strip.x + 16f, strip.y + 28f, Mathf.Max(0f, strip.width - 32f), 48f);
            float contentWidth = _tools.Length * 52f - 8f;
            float maxScroll = Mathf.Max(0f, contentWidth - ToolbarViewport.width);
            _toolbarScroll = Mathf.Clamp(_toolbarScroll, 0f, maxScroll);
            HandleToolbarInput(ToolbarViewport, maxScroll);

            GUIStyle entry = new GUIStyle(button)
            {
                fixedHeight = 44f, fixedWidth = 44f,
                imagePosition = ImagePosition.ImageOnly,
                padding = new RectOffset(10, 10, 10, 10)
            };
            URflowVisuals.StyleButton(entry);
            GUI.BeginGroup(ToolbarViewport);
            try
            {
                for (int i = 0; i < _tools.Length; i++)
                {
                    ToolEntry tool = _tools[i];
                    Rect tile = new Rect(i * 52f - _toolbarScroll, 0f, 44f, 44f);
                    if (i == 0) FirstToolRect = new Rect(ToolbarViewport.x + tile.x, ToolbarViewport.y, 44f, 44f);
                    if (tile.xMax < 0f || tile.x > ToolbarViewport.width) continue;
                    Texture icon = EditorGUIUtility.IconContent(tool.Icon).image;
                    if (GUI.Button(tile, new GUIContent(icon, text(tool.English, tool.Chinese)), entry)) tool.Open();
                }
            }
            finally { GUI.EndGroup(); }
        }

        private void HandleToolbarInput(Rect viewport, float maxScroll)
        {
            Event current = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (!GUI.enabled || maxScroll <= 0f)
            {
                if (_dragging && GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
                _trackingDrag = _dragging = false;
                return;
            }
            if (current.type == EventType.ScrollWheel && viewport.Contains(current.mousePosition))
            {
                float next = Mathf.Clamp(_toolbarScroll + (current.delta.x + current.delta.y) * 22f, 0f, maxScroll);
                if (!Mathf.Approximately(next, _toolbarScroll))
                { _toolbarScroll = next; current.Use(); Repaint(); }
            }
            if (current.type == EventType.MouseDown && current.button == 0 && viewport.Contains(current.mousePosition))
            {
                _trackingDrag = true;
                _dragOrigin = current.mousePosition;
                _dragScrollStart = _toolbarScroll;
            }
            else if (current.type == EventType.MouseDrag && _trackingDrag)
            {
                if (!_dragging && Mathf.Abs(current.mousePosition.x - _dragOrigin.x) > 6f)
                { _dragging = true; GUIUtility.hotControl = control; }
                if (_dragging)
                {
                    _toolbarScroll = Mathf.Clamp(_dragScrollStart - current.mousePosition.x + _dragOrigin.x, 0f, maxScroll);
                    current.Use(); Repaint();
                }
            }
            else if (current.type == EventType.MouseUp && current.button == 0)
            {
                if (_dragging)
                { if (GUIUtility.hotControl == control) GUIUtility.hotControl = 0; current.Use(); }
                _trackingDrag = _dragging = false;
            }
        }

        private void Open()
        {
            BeginTransition(true);
            StartSweep();
            _panelError = null;
            _panel.Refresh(true);
            GUI.FocusControl(null);
            GUIUtility.ExitGUI();
        }

        public void DrawPanel(Rect area, GUIStyle button, GUIStyle selected,
            GUIStyle primary, Func<string, string, string> text)
        {
            if (!IsOpen) { PanelRect = new Rect(); return; }
            if (GUI.enabled && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { Close(); Event.current.Use(); }

            float height = Mathf.Min(420f, area.height);
            UpdateProgress();
            // Match the approved preview: a subtle 24px rise, not a full-height slide.
            Rect sheet = new Rect(0f, area.height - height + 24f * (1f - _progress), area.width, height);
            PanelRect = new Rect(area.x + sheet.x, area.y + sheet.y, sheet.width, sheet.height);
            _panel.SetLayoutSize(sheet.size);
            Rect animationClip = new Rect(area.x, area.y, area.width, area.height + 24f);
            Color previousColor = GUI.color;
            EventType previousEventType = Event.current.type;
            bool hideClosingInput = !_open.target && previousEventType != EventType.Layout &&
                previousEventType != EventType.Repaint;
            // A closing sheet remains visible until its alpha reaches zero. Ignore input
            // without dimming its controls, so disabling interaction does not cause a flash.
            if (hideClosingInput) Event.current.type = EventType.Ignore;
            GUI.color = previousColor * new Color(1f, 1f, 1f, _progress);
            GUI.BeginGroup(animationClip);
            try
            {
                URflowVisuals.DrawSurface(sheet, URflowVisuals.PanelBackground, URflowVisuals.Divider);
                URflowVisuals.DrawPanelEdge(sheet);
                if (URflowVisuals.DrawEdgeSweep(sheet, _sweepStarted)) Repaint();
                GUILayout.BeginArea(sheet);
                try
                {
                    if (string.IsNullOrEmpty(_panelError)) _panel.Draw(button, selected, primary, text, Close);
                    else
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            GUILayout.Space(16f);
                            EditorGUILayout.HelpBox(text("Unable to draw the tool panel: ", "无法绘制工具面板：") +
                                _panelError, MessageType.Error);
                            if (GUILayout.Button(text("Close", "关闭"), button, GUILayout.Height(30f))) Close();
                        }
                    }
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception)
                { _panelError = exception.Message; Repaint(); GUIUtility.ExitGUI(); }
                finally { GUILayout.EndArea(); }
            }
            finally
            {
                GUI.EndGroup();
                GUI.color = previousColor;
                if (hideClosingInput) Event.current.type = previousEventType;
            }
        }

        private void BeginTransition(bool target)
        {
            UpdateProgress();
            _transitionFrom = _progress;
            _transitionTarget = target;
            _transitionStarted = EditorApplication.timeSinceStartup;
            _transitioning = true;
            _open.value = target;
            Repaint();
        }

        private void SynchronizeExternalState()
        {
            // Validation and Unity lifecycle restoration can set the AnimBool value directly.
            if (_transitioning)
            {
                if (_open.target != _transitionTarget)
                {
                    _transitioning = false;
                    _progress = _open.target ? 1f : 0f;
                    _transitionTarget = _open.target;
                }
                return;
            }
            if (_open.isAnimating) return;
            float endpoint = _open.target ? 1f : 0f;
            if (!Mathf.Approximately(_progress, endpoint))
            { _transitioning = false; _progress = endpoint; _transitionTarget = _open.target; }
        }

        private void UpdateProgress()
        {
            if (!_transitioning) return;
            float elapsed = (float)(EditorApplication.timeSinceStartup - _transitionStarted);
            float duration = _transitionTarget ? URflowVisuals.PanelOpenSeconds : PanelCloseSeconds;
            float t = Mathf.Clamp01(elapsed / duration);
            // Keep the approved opening curve. Closing uses a symmetric ease-in/out
            // with opacity, rather than the opening's rapid ease-out followed by a cut.
            float eased = _transitionTarget
                ? CubicBezierConverter.Evaluate(0.2f, 0.75f, 0.3f, 1f, t)
                : Mathf.SmoothStep(0f, 1f, t);
            _progress = Mathf.Lerp(_transitionFrom, _transitionTarget ? 1f : 0f, eased);
            if (t >= 1f)
            {
                _transitioning = false;
                _progress = _transitionTarget ? 1f : 0f;
                _open.value = _transitionTarget;
            }
        }

        private void Tick()
        {
            SynchronizeExternalState();
            bool wasTransitioning = _transitioning;
            UpdateProgress();
            bool sweepActive = IsOpen && EditorApplication.timeSinceStartup - _sweepStarted < URflowVisuals.EdgeSweepSeconds;
            if (wasTransitioning || sweepActive) Repaint();
        }

        private void StartSweep() { _sweepStarted = EditorApplication.timeSinceStartup; Repaint(); }

        private void Close()
        {
            BeginTransition(false);
            _panel.CancelPending();
            GUI.FocusControl(null);
            GUIUtility.hotControl = 0;
            Repaint();
        }

        public void Dispose()
        {
            _disposed = true;
            _open.valueChanged.RemoveListener(Repaint);
            Undo.undoRedoPerformed -= RefreshIfOpen;
            EditorApplication.update -= Tick;
            _panel.Dispose();
        }
    }
}
