using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    internal sealed class URflowKeyframeMovePanel : IDisposable
    {
        private readonly Action _repaint;
        private URflowKeyframeMoveAnchor _anchor = URflowKeyframeMoveAnchor.Last;
        private URflowKeyframeMoveMode _mode = URflowKeyframeMoveMode.Translate;
        private URflowKeyframeMovePreview _preview;
        private Vector2 _scroll;
        private string _error;
        private string _notice;
        private int? _modifiedCount;
        private bool _pending;
        private bool _disposed;

        public URflowKeyframeMovePanel(Action repaint) { _repaint = repaint; }

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
                try
                {
                    int count;
                    if (URflowKeyframeMoveService.TryApply(plan, out count, out _error))
                    {
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
                if (string.IsNullOrEmpty(_error)) Refresh();
            };
        }

        public void Draw(GUIStyle button, GUIStyle selected, GUIStyle primary,
            Func<string, string, string> text, Action close)
        {
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            try
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(10f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16f);
                        GUILayout.Label(text("Keyframe Move", "关键帧移动"), Label(true));
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("×", text("Close", "关闭")), button,
                            GUILayout.Width(26f), GUILayout.Height(24f))) close();
                        GUILayout.Space(16f);
                    }
                    GUILayout.Space(6f);
                    using (var scrollView = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.ExpandHeight(true)))
                    {
                        _scroll = scrollView.scrollPosition;
                        DrawBody(button, selected, text);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16f);
                        using (new EditorGUI.DisabledScope(_pending))
                        {
                            if (GUILayout.Button(text("Refresh", "刷新"), button,
                                GUILayout.Width(60f), GUILayout.Height(30f))) { _modifiedCount = null; Refresh(true); }
                        }
                        using (new EditorGUI.DisabledScope(_pending || _preview == null || !_preview.IsValid))
                        {
                            if (GUILayout.Button(text("Apply Move", "应用移动"), primary,
                                GUILayout.Height(30f), GUILayout.ExpandWidth(true))) Apply();
                        }
                        GUILayout.Space(16f);
                    }
                    GUILayout.Space(8f);
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
                    GUILayout.Label(text("Align to", "对齐基准"), Label());
                    using (new EditorGUI.DisabledScope(_pending))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Toggle(_anchor == URflowKeyframeMoveAnchor.First,
                                text("First Frame", "首帧"), _anchor == URflowKeyframeMoveAnchor.First ? selected : button,
                                GUILayout.Height(28f)) && _anchor != URflowKeyframeMoveAnchor.First)
                            { _anchor = URflowKeyframeMoveAnchor.First; _modifiedCount = null; Refresh(true); }
                            if (GUILayout.Toggle(_anchor == URflowKeyframeMoveAnchor.Last,
                                text("Last Frame", "尾帧"), _anchor == URflowKeyframeMoveAnchor.Last ? selected : button,
                                GUILayout.Height(28f)) && _anchor != URflowKeyframeMoveAnchor.Last)
                            { _anchor = URflowKeyframeMoveAnchor.Last; _modifiedCount = null; Refresh(true); }
                        }
                        GUILayout.Space(6f);
                        bool ratio = EditorGUILayout.ToggleLeft(text("Proportional adjustment", "按比例调整"),
                            _mode == URflowKeyframeMoveMode.Proportional, Label());
                        if (ratio != (_mode == URflowKeyframeMoveMode.Proportional))
                        {
                            _mode = ratio ? URflowKeyframeMoveMode.Proportional : URflowKeyframeMoveMode.Translate;
                            _modifiedCount = null;
                            Refresh(true);
                        }
                    }
                    GUILayout.Space(8f);
                    if (_pending) GUILayout.Label(text("Reading static values…", "正在读取静态值…"), Label());
                    if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Warning);
                    if (_preview != null)
                    {
                        string context = (_preview.Root != null ? _preview.Root.name : "") + " / " +
                            (_preview.Clip != null ? _preview.Clip.name : "");
                        GUILayout.Label(context, Label(false, true), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
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
                        "仅移动选中属性的全部关键帧。退出动画预览后读取静态值。"), Label(false, true), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                    GUILayout.Space(6f);
                }
                GUILayout.Space(16f);
            }
        }

        private void DrawRows(Func<string, string, string> text)
        {
            bool proportional = _mode == URflowKeyframeMoveMode.Proportional;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(text("Property", "属性"), Label(), GUILayout.MinWidth(62f));
                GUILayout.Label(text("Anchor", "基准值"), Numeric(), GUILayout.Width(56f));
                GUILayout.Label(text("Static", "默认值"), Numeric(), GUILayout.Width(56f));
                GUILayout.Label(proportional ? text("Factor", "倍率") : text("Offset", "偏移"),
                    Numeric(), GUILayout.Width(56f));
            }
            foreach (URflowKeyframeMovePreviewRow row in _preview.Rows)
            {
                Rect separator = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(separator, new Color(0.25f, 0.25f, 0.25f));
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent(PropertyLabel(row.Binding, text), row.Label + " / " + row.Binding.propertyName),
                        Label(), GUILayout.MinWidth(62f), GUILayout.ExpandWidth(true));
                    GUILayout.Label(row.Result == null ? new GUIContent("—") : ValueContent(row.Result.OriginalAnchorValue), Numeric(), GUILayout.Width(56f));
                    GUILayout.Label(row.Result == null ? new GUIContent("—") : ValueContent(row.Result.TargetValue), Numeric(), GUILayout.Width(56f));
                    GUILayout.Label(row.Result == null ? new GUIContent("—") : ValueContent(row.Result.DeltaOrFactor), Numeric(), GUILayout.Width(56f));
                }
                if (!string.IsNullOrEmpty(row.Binding.path))
                    GUILayout.Label(new GUIContent(row.Binding.path, row.Binding.path), Label(false, true),
                        GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                if (!string.IsNullOrEmpty(row.Error)) EditorGUILayout.HelpBox(row.Error, MessageType.Warning);
                if (row.Result == null) continue;
                if (row.Result.IsZeroFactor) EditorGUILayout.HelpBox(text("Factor 0 removes all motion on this property.",
                    "倍率为 0：该属性的动画变化会归零。"), MessageType.Warning);
                else if (row.Result.IsNegativeFactor) EditorGUILayout.HelpBox(text("Negative factor reverses values and motion direction.",
                    "负倍率：数值符号与运动方向会反转。"), MessageType.Warning);
                Keyframe[] keys = row.Result.Curve.keys;
                string range = keys.Length == 1 ? Number(keys[0].value) :
                    Number(keys[0].value) + " → " + Number(keys[keys.Length - 1].value);
                GUILayout.Label(text("Result: ", "调整后：") + range + "  (" + keys.Length + " " +
                    text("keys", "帧") + ")", Label(false, true), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                GUILayout.Space(6f);
            }
        }

        private static GUIStyle Label(bool bold = false, bool wrap = false)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = wrap,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            return style;
        }

        private static GUIStyle Numeric()
        {
            GUIStyle style = Label();
            style.alignment = TextAnchor.MiddleRight;
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
