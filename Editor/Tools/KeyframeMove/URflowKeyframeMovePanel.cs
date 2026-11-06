using System;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    // The shared sheet owns layout; scanning, filtering and checks stay in the scan panel.
    internal sealed class URflowKeyframeMovePanel : IDisposable
    {
        private readonly URflowKeyframeMoveScanPanel _scanPanel;
        private Vector2 _scroll;
        private bool _disposed;
        private Vector2 _layoutSize = new Vector2(296f, 420f);
        internal Rect ActionsRect { get; private set; }
        internal Rect CloseRect { get; private set; }
        internal Rect BodyRect { get; private set; }

        internal void SetLayoutSize(Vector2 size) { _layoutSize = size; }

        public URflowKeyframeMovePanel(Action repaint, Action onApplied = null)
        { _scanPanel = new URflowKeyframeMoveScanPanel(repaint, onApplied); }

        public void Refresh(bool clearNotice = false)
        { if (!_disposed) _scanPanel.Refresh(clearNotice); }

        internal void CancelPending() { _scanPanel.CancelPending(); }

        public void Draw(GUIStyle button, GUIStyle selected, GUIStyle primary,
            Func<string, string, string> text, Action close)
        {
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            GUIStyle secondaryButton = Button(button);
            GUIStyle selectedButton = Button(selected, false, true);
            GUIStyle primaryButton = Button(primary, true);
            GUIStyle closeButton = Button(button);
            closeButton.fontSize = 16;
            closeButton.padding = new RectOffset(0, 0, 0, 0);
            try
            {
                float width = _layoutSize.x;
                float height = _layoutSize.y;
                GUIStyle heading = new GUIStyle(EditorStyles.label)
                { fontSize = 15, fontStyle = FontStyle.Bold };
                heading.normal.textColor = new Color(.87f, .88f, .9f);
                GUI.Label(new Rect(16f, 12f, width - 74f, 24f), text("Keyframe Move", "关键帧移动"), heading);
                CloseRect = new Rect(width - 42f, 12f, 26f, 24f);
                if (GUI.Button(CloseRect, new GUIContent("×", text("Close", "关闭")), closeButton)) close();
                DrawDivider(new Rect(0f, 50f, width, 1f));
                // One page: the anchor and result list use the same scan and explicit check list.
                BodyRect = new Rect(0f, 56f, width, Mathf.Max(0f, height - 118f));
                GUILayout.BeginArea(BodyRect);
                try
                {
                    using (var scrollView = new EditorGUILayout.ScrollViewScope(_scroll,
                        GUILayout.Height(BodyRect.height), GUILayout.MinHeight(0f)))
                    {
                        _scroll = scrollView.scrollPosition;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(16f);
                            using (new EditorGUILayout.VerticalScope())
                            {
                                GUILayout.Space(8f);
                                _scanPanel.DrawBody(secondaryButton, selectedButton, text);
                            }
                            GUILayout.Space(16f);
                        }
                    }
                }
                finally { GUILayout.EndArea(); }
                DrawDivider(new Rect(0f, height - 62f, width, 1f));
                ActionsRect = new Rect(16f, height - 46f, width - 32f, 34f);
                using (new EditorGUI.DisabledScope(_scanPanel.IsPending))
                {
                    if (GUI.Button(new Rect(ActionsRect.x, ActionsRect.y, 68f, 34f),
                        text("Rescan", "扫描"), secondaryButton)) Refresh(true);
                }
                using (new EditorGUI.DisabledScope(!_scanPanel.CanApply))
                {
                    if (GUI.Button(new Rect(ActionsRect.x + 72f, ActionsRect.y, ActionsRect.width - 72f, 34f),
                        text("Apply", "应用") + " (" + _scanPanel.SelectedCount + ")", primaryButton)) _scanPanel.Apply();
                }
            }
            finally { GUI.color = oldColor; GUI.enabled = oldEnabled; }
        }

        private static void DrawDivider(Rect rect)
        {
            Color color = URflowVisuals.Divider;
            color.a *= GUI.color.a;
            EditorGUI.DrawRect(rect, color);
        }

        private static GUIStyle Button(GUIStyle source, bool primary = false, bool selected = false)
        {
            GUIStyle style = new GUIStyle(source);
            if (selected) URflowVisuals.StyleSelectedButton(style);
            else URflowVisuals.StyleButton(style, primary);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = primary ? 13 : 11;
            style.alignment = TextAnchor.MiddleCenter;
            style.padding = new RectOffset(8, 8, 4, 4);
            return style;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scanPanel.Dispose();
        }
    }
}
