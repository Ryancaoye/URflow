using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    internal sealed class URflowKeyframeMovePanel : IDisposable
    {
        private readonly Action _repaint;
        private readonly Action _onApplied;
        private URflowKeyframeMoveAnchor _anchor = URflowKeyframeMoveAnchor.Last;
        private URflowKeyframeMoveMode _mode = URflowKeyframeMoveMode.Translate;
        private URflowKeyframeMovePreview _preview;
        private Vector2 _scroll;
        private string _error;
        private string _notice;
        private int? _modifiedCount;
        private bool _pending;
        private bool _disposed;

        public URflowKeyframeMovePanel(Action repaint, Action onApplied = null)
        { _repaint = repaint; _onApplied = onApplied; }

        public void Refresh(bool clearNotice = false)
        {
            if (_pending || _disposed) return;
            if (clearNotice) _notice = null;
            _pending = true;
            _preview = null;
            _error = null;
            EditorApplication.delayCall += ReadPreview;
            _repaint();
        }

        private void ReadPreview()
        {
            if (_disposed) return;
            try
            {
                URflowKeyframeMoveService.TryCreatePreview(_anchor, _mode, out _preview, out _error);
                if (_preview != null && !string.IsNullOrEmpty(_preview.Notice)) _notice = _preview.Notice;
            }
            catch (ExitGUIException) { throw; }
            catch (Exception ex) { _error = ex.Message; _preview = null; }
            finally { _pending = false; _repaint(); }
        }

        private void Apply()
        {
            if (_pending || _preview == null || !_preview.IsValid) return;
            URflowKeyframeMovePreview plan = _preview;
            _pending = true;
            EditorApplication.delayCall += () =>
            {
                if (_disposed) return;
                bool applied = false;
                try
                {
                    int count;
                    if (URflowKeyframeMoveService.TryApply(plan, out count, out _error))
                    {
                        applied = true;
                        _modifiedCount = count;
                        _preview = null;
                    }
                }
                catch (ExitGUIException) { throw; }
                catch (Exception ex) { _error = ex.Message; }
                finally
                {
                    if (!string.IsNullOrEmpty(plan.Notice)) _notice = plan.Notice;
                    _pending = false;
                    _repaint();
                }
                if (applied && _onApplied != null)
                {
                    try { _onApplied(); }
                    catch (ExitGUIException) { throw; }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                if (string.IsNullOrEmpty(_error)) Refresh();
            };
        }

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
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(12f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16f);
                        GUIStyle heading = Label(true);
                        heading.fontSize = 15;
                        GUILayout.Label(text("Keyframe Move", "关键帧移动"), heading, GUILayout.Height(24f));
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("×", text("Close", "关闭")), closeButton,
                            GUILayout.Width(26f), GUILayout.Height(24f))) close();
                        GUILayout.Space(16f);
                    }
                    GUILayout.Space(9f);
                    DrawDivider();
                    using (var scrollView = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.ExpandHeight(true)))
                    {
                        _scroll = scrollView.scrollPosition;
                        DrawBody(secondaryButton, selectedButton, text);
                    }
                    DrawDivider();
                    GUILayout.Space(10f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16f);
                        using (new EditorGUI.DisabledScope(_pending))
                        {
                            if (GUILayout.Button(text("Refresh", "刷新"), secondaryButton,
                                GUILayout.Width(68f), GUILayout.Height(34f))) { _modifiedCount = null; Refresh(true); }
                        }
                        GUILayout.Space(4f);
                        using (new EditorGUI.DisabledScope(_pending || _preview == null || !_preview.IsValid))
                        {
                            if (GUILayout.Button(text("Apply Move", "应用移动"), primaryButton,
                                GUILayout.Height(34f), GUILayout.ExpandWidth(true))) Apply();
                        }
                        GUILayout.Space(16f);
                    }
                    GUILayout.Space(12f);
                }
            }
            finally { GUI.color = oldColor; GUI.enabled = oldEnabled; }
        }

        private void DrawBody(GUIStyle button, GUIStyle selected, Func<string, string, string> text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16f);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(12f);
                    GUILayout.Label(text("ALIGN TO", "对齐基准"), SectionLabel());
                    GUILayout.Space(4f);
                    using (new EditorGUI.DisabledScope(_pending))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Toggle(_anchor == URflowKeyframeMoveAnchor.First,
                                text("First Frame", "首帧"), _anchor == URflowKeyframeMoveAnchor.First ? selected : button,
                                GUILayout.Height(32f)) && _anchor != URflowKeyframeMoveAnchor.First)
                            { _anchor = URflowKeyframeMoveAnchor.First; _modifiedCount = null; Refresh(true); }
                            GUILayout.Space(4f);
                            if (GUILayout.Toggle(_anchor == URflowKeyframeMoveAnchor.Last,
                                text("Last Frame", "尾帧"), _anchor == URflowKeyframeMoveAnchor.Last ? selected : button,
                                GUILayout.Height(32f)) && _anchor != URflowKeyframeMoveAnchor.Last)
                            { _anchor = URflowKeyframeMoveAnchor.Last; _modifiedCount = null; Refresh(true); }
                        }
                        GUILayout.Space(9f);
                        bool ratio = EditorGUILayout.ToggleLeft(text("Proportional adjustment", "按比例调整"),
                            _mode == URflowKeyframeMoveMode.Proportional, Label(), GUILayout.Height(20f));
                        if (ratio != (_mode == URflowKeyframeMoveMode.Proportional))
                        {
                            _mode = ratio ? URflowKeyframeMoveMode.Proportional : URflowKeyframeMoveMode.Translate;
                            _modifiedCount = null;
                            Refresh(true);
                        }
                    }
                    GUILayout.Space(12f);
                    GUILayout.Label(text("PREVIEW", "预览"), SectionLabel());
                    GUILayout.Space(4f);
                    if (_pending) GUILayout.Label(text("Reading static values…", "正在读取静态值…"), Label(false, true, true));
                    if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Warning);
                    if (_preview != null)
                    {
                        string context = (_preview.Root != null ? _preview.Root.name : "") + " / " +
                            (_preview.Clip != null ? _preview.Clip.name : "");
                        GUILayout.Label(context, Label(false, true, true), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                        GUILayout.Space(6f);
                        DrawRows(text);
                    }
                    if (!string.IsNullOrEmpty(_notice)) EditorGUILayout.HelpBox(_notice, MessageType.Info);
                    if (_modifiedCount.HasValue)
                    {
                        string status = _modifiedCount.Value > 0
                            ? text("Moved ", "已移动 ") + _modifiedCount.Value + text(" property curves.", " 条属性曲线。")
                            : text("Already aligned; no changes needed.", "已经对齐，无需修改。");
                        EditorGUILayout.HelpBox(status, MessageType.Info);
                    }
                    GUILayout.Space(8f);
                    GUILayout.Label(text("Selected properties: all keys. Static values are read after exiting Animation preview.",
                        "仅移动选中属性的全部关键帧。退出动画预览后读取静态值。"), Label(false, true, true),
                        GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                    GUILayout.Space(10f);
                }
                GUILayout.Space(16f);
            }
        }

        private void DrawRows(Func<string, string, string> text)
        {
            bool proportional = _mode == URflowKeyframeMoveMode.Proportional;
            GUIStyle card = new GUIStyle();
            URflowVisuals.StyleButton(card);
            card.padding = new RectOffset(10, 10, 9, 9);
            foreach (URflowKeyframeMovePreviewRow row in _preview.Rows)
            {
                using (new EditorGUILayout.VerticalScope(card))
                {
                    GUILayout.Label(new GUIContent(PropertyLabel(row.Binding, text), row.Label + " / " + row.Binding.propertyName),
                        Label(true), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                    if (!string.IsNullOrEmpty(row.Binding.path))
                        GUILayout.Label(new GUIContent(row.Binding.path, row.Binding.path), Label(false, true, true),
                            GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                    GUILayout.Space(6f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawValue(text("ANCHOR", "基准值"), row.Result == null ? null : ValueContent(row.Result.OriginalAnchorValue));
                        DrawValue(text("STATIC", "默认值"), row.Result == null ? null : ValueContent(row.Result.TargetValue));
                        DrawValue(proportional ? text("FACTOR", "倍率") : text("OFFSET", "偏移"),
                            row.Result == null ? null : ValueContent(row.Result.DeltaOrFactor));
                    }
                    if (!string.IsNullOrEmpty(row.Error)) EditorGUILayout.HelpBox(row.Error, MessageType.Warning);
                    if (row.Result != null)
                    {
                        if (row.Result.IsZeroFactor) EditorGUILayout.HelpBox(text("Factor 0 removes all motion on this property.",
                            "倍率为 0：该属性的动画变化会归零。"), MessageType.Warning);
                        else if (row.Result.IsNegativeFactor) EditorGUILayout.HelpBox(text("Negative factor reverses values and motion direction.",
                            "负倍率：数值符号与运动方向会反转。"), MessageType.Warning);
                        Keyframe[] keys = row.Result.Curve.keys;
                        string range = keys.Length == 1 ? Number(keys[0].value) :
                            Number(keys[0].value) + " → " + Number(keys[keys.Length - 1].value);
                        GUILayout.Space(5f);
                        GUILayout.Label(text("Result: ", "调整后：") + range + "  (" + keys.Length + " " +
                            text("keys", "帧") + ")", Label(false, true, true), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                    }
                }
                GUILayout.Space(6f);
            }
        }

        private static void DrawValue(string name, GUIContent value)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label(name, SectionLabel(), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                GUIStyle numeric = Label(true);
                numeric.fontSize = 12;
                GUILayout.Label(value ?? new GUIContent("—"), numeric, GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
            }
        }

        private static void DrawDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, URflowVisuals.Divider);
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

        private static GUIStyle Label(bool bold = false, bool wrap = false, bool muted = false)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = wrap,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = muted ? new Color(.56f, .58f, .62f) : new Color(.87f, .88f, .9f);
            return style;
        }

        private static GUIStyle SectionLabel()
        {
            GUIStyle style = Label(false, false, true);
            style.fontSize = 9;
            return style;
        }

        private static string PropertyLabel(EditorCurveBinding binding, Func<string, string, string> text)
        {
            string name = binding.propertyName ?? "";
            string axis = name.Length > 0 ? name.Substring(name.LastIndexOf('.') + 1).ToUpperInvariant() : "";
            if (name.StartsWith("m_LocalScale.", StringComparison.Ordinal)) return text("Scale ", "缩放 ") + axis;
            if (name.StartsWith("m_AnchoredPosition.", StringComparison.Ordinal)) return text("UI Pos ", "UI 位置 ") + axis;
            if (name.StartsWith("m_LocalPosition.", StringComparison.Ordinal)) return text("Position ", "位置 ") + axis;
            return name;
        }

        private static GUIContent ValueContent(float value)
        {
            return new GUIContent(Number(value), value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string Number(float value) { return value.ToString("G6", CultureInfo.InvariantCulture); }

        public void Dispose()
        {
            _disposed = true;
            EditorApplication.delayCall -= ReadPreview;
        }
    }
}
