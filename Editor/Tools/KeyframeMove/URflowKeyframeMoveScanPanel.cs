using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    // Owns batch selection/filtering only. Clip reads and writes belong to the scan service.
    internal sealed class URflowKeyframeMoveScanPanel : IDisposable
    {
        private readonly Action _repaint;
        private readonly Action _onApplied;
        private readonly HashSet<URflowKeyframeMoveScanRow> _selected = new HashSet<URflowKeyframeMoveScanRow>();
        private URflowKeyframeMoveMode _mode = URflowKeyframeMoveMode.Translate;
        private URflowKeyframeMoveAnchor _anchor = URflowKeyframeMoveAnchor.Last;
        private URflowKeyframeMoveScanPreview _preview;
        private readonly HashSet<string> _selectionKeys = new HashSet<string>(StringComparer.Ordinal);
        private bool _selectionOnly;
        private string _selectionError;
        private string _search = string.Empty;
        private bool _differencesOnly = true;
        private string _error;
        private string _notice;
        private int? _modifiedCount;
        private bool _pending;
        private bool _disposed;
        private EditorApplication.CallbackFunction _applyCallback;

        internal bool IsPending { get { return _pending; } }
        internal int SelectedCount { get { return _selected.Count; } }
        internal Rect FirstAnchorRect { get; private set; }
        internal Rect LastAnchorRect { get; private set; }
        internal Rect RatioRect { get; private set; }
        internal Rect SelectionOnlyRect { get; private set; }
        internal Rect DifferencesOnlyRect { get; private set; }
        internal bool CanApply
        {
            get { return !_disposed && !_pending && _preview != null && _preview.IsValid && _selected.Count > 0; }
        }

        public URflowKeyframeMoveScanPanel(Action repaint, Action onApplied)
        { _repaint = repaint; _onApplied = onApplied; }

        public void Refresh(bool clearNotice = false)
        {
            if (_pending || _disposed) return;
            if (clearNotice) { _notice = null; _modifiedCount = null; }
            _selected.Clear();
            _preview = null;
            _error = null;
            _pending = true;
            EditorApplication.delayCall += ReadPreview;
            _repaint();
        }

        private void ReadPreview()
        {
            if (_disposed) return;
            try
            {
                URflowKeyframeMoveScanService.TryCreatePreview(_anchor, _mode, out _preview, out _error);
                CaptureSelection();
                if (_preview != null && !string.IsNullOrEmpty(_preview.Notice)) _notice = _preview.Notice;
            }
            catch (ExitGUIException) { throw; }
            catch (Exception exception) { _error = exception.Message; _preview = null; }
            finally { _pending = false; _repaint(); }
        }

        private void CaptureSelection()
        {
            _selectionKeys.Clear();
            _selectionError = null;
            if (!_selectionOnly || _preview == null || !_preview.IsValid) return;
            List<string> keys;
            if (URflowKeyframeMoveScanService.TryGetSelectedBindingKeys(_preview, out keys, out _selectionError))
                foreach (string key in keys) _selectionKeys.Add(key);
        }

        private void ReadSelection()
        {
            if (_disposed) return;
            try { CaptureSelection(); }
            catch (ExitGUIException) { throw; }
            catch (Exception exception) { _selectionError = exception.Message; _selectionKeys.Clear(); }
            finally { _pending = false; _repaint(); }
        }

        public void Apply()
        {
            if (!CanApply) return;
            URflowKeyframeMoveScanPreview plan = _preview;
            bool selectionOnly = _selectionOnly;
            // Freeze the explicit check list before leaving OnGUI, including hidden checked rows.
            var rows = new List<URflowKeyframeMoveScanRow>();
            foreach (URflowKeyframeMoveScanRow row in plan.Rows)
                if (_selected.Contains(row)) rows.Add(row);
            _pending = true;
            _applyCallback = delegate
            {
                _applyCallback = null;
                if (_disposed) return;
                bool applied = false;
                try
                {
                    if (selectionOnly)
                    {
                        List<string> currentKeys;
                        if (!URflowKeyframeMoveScanService.TryGetSelectedBindingKeys(plan, out currentKeys, out _error)) return;
                        var currentSelection = new HashSet<string>(currentKeys, StringComparer.Ordinal);
                        foreach (URflowKeyframeMoveScanRow row in rows)
                            if (!currentSelection.Contains(URflowKeyframeMoveService.BindingKey(row.Binding)))
                            {
                                _error = "Animation 属性选择已改变，请重新扫描。";
                                _selected.Clear();
                                return;
                            }
                    }
                    int count;
                    if (URflowKeyframeMoveScanService.TryApply(plan, rows, out count, out _error))
                    {
                        applied = true;
                        _modifiedCount = count;
                        _selected.Clear();
                        _preview = null;
                    }
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { _error = exception.Message; }
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
                    catch (Exception exception) { Debug.LogException(exception); }
                }
                if (applied) Refresh();
            };
            EditorApplication.delayCall += _applyCallback;
            _repaint();
        }

        public void DrawBody(GUIStyle button, GUIStyle selected, Func<string, string, string> text)
        {
            using (new EditorGUI.DisabledScope(_pending))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(text("First Frame", "首帧"),
                        text("Match the first keyframe to the static layout value.", "让首帧对齐静态布局值。")),
                        _anchor == URflowKeyframeMoveAnchor.First ? selected : button,
                        GUILayout.Height(32f), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true))
                        && _anchor != URflowKeyframeMoveAnchor.First)
                    { _anchor = URflowKeyframeMoveAnchor.First; Refresh(true); GUIUtility.ExitGUI(); }
                    FirstAnchorRect = GUILayoutUtility.GetLastRect();
                    GUILayout.Space(4f);
                    if (GUILayout.Button(new GUIContent(text("Last Frame", "尾帧"),
                        text("Match the last keyframe to the static layout value.", "让尾帧对齐静态布局值。")),
                        _anchor == URflowKeyframeMoveAnchor.Last ? selected : button,
                        GUILayout.Height(32f), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true))
                        && _anchor != URflowKeyframeMoveAnchor.Last)
                    { _anchor = URflowKeyframeMoveAnchor.Last; Refresh(true); GUIUtility.ExitGUI(); }
                    LastAnchorRect = GUILayoutUtility.GetLastRect();
                }
                GUILayout.Space(6f);
                bool ratio = EditorGUILayout.ToggleLeft(new GUIContent(text("Proportional", "按比例"),
                    text("Scale every keyframe value by the same factor. When off, shift all values by the same amount.",
                        "勾选时，所有关键帧数值按同一倍率调整；关闭时，所有数值整体平移。")),
                    _mode == URflowKeyframeMoveMode.Proportional, Label(), GUILayout.Height(20f));
                RatioRect = GUILayoutUtility.GetLastRect();
                if (ratio != (_mode == URflowKeyframeMoveMode.Proportional))
                {
                    _mode = ratio ? URflowKeyframeMoveMode.Proportional : URflowKeyframeMoveMode.Translate;
                    Refresh(true);
                    GUIUtility.ExitGUI();
                }
                GUILayout.Space(6f);
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                string search;
                try
                {
                    EditorGUIUtility.labelWidth = 52f;
                    search = EditorGUILayout.TextField(new GUIContent(text("Search", "搜索"),
                        text("Search object path or property", "搜索对象路径或属性")), _search, GUILayout.MinWidth(0f));
                }
                finally { EditorGUIUtility.labelWidth = previousLabelWidth; }
                if (search != _search) { _search = search; GUIUtility.ExitGUI(); }
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool differencesOnly = EditorGUILayout.ToggleLeft(new GUIContent(text("Differences", "只看差异"),
                        text("Show changed or unavailable properties only.", "仅显示有差异或无法处理的属性。")),
                        _differencesOnly, Label(), GUILayout.Height(20f), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                    DifferencesOnlyRect = GUILayoutUtility.GetLastRect();
                    if (differencesOnly != _differencesOnly) { _differencesOnly = differencesOnly; GUIUtility.ExitGUI(); }
                    bool selectionOnly = EditorGUILayout.ToggleLeft(new GUIContent(
                        text("Animation selection", "Animation 选中项"),
                        text("Show the selected position, scale and Size Delta properties in Animation. Rescan after changing selection. Changing this scope clears checks.",
                            "仅显示 Animation 选中的位置、缩放和 Size Delta 属性；改变属性选择后请重新扫描。切换此范围会清空勾选。")),
                        _selectionOnly, Label(), GUILayout.Height(20f), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                    SelectionOnlyRect = GUILayoutUtility.GetLastRect();
                    if (selectionOnly != _selectionOnly)
                    {
                        _selectionOnly = selectionOnly;
                        _selected.Clear();
                        _selectionKeys.Clear();
                        _selectionError = null;
                        _pending = true;
                        EditorApplication.delayCall += ReadSelection;
                        _repaint();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            GUILayout.Space(6f);
            if (_pending) GUILayout.Label(text("Scanning…", "扫描中…"), Label(false, true, true));
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(Message(_error, text), MessageType.Warning);
            if (!string.IsNullOrEmpty(_selectionError)) EditorGUILayout.HelpBox(Message(_selectionError, text), MessageType.Warning);
            if (_preview != null)
            {
                string context = (_preview.Root != null ? _preview.Root.name : "") + " / " +
                    (_preview.Clip != null ? _preview.Clip.name : "");
                GUILayout.Label(new GUIContent(_preview.Clip != null ? _preview.Clip.name : context, context), Label(true), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                DrawSummary(text);
                GUILayout.Space(6f);
                List<URflowKeyframeMoveScanRow> visible = VisibleRows(text);
                using (new EditorGUI.DisabledScope(_pending || !_preview.IsValid))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent(text("Select Shown", "全选结果"),
                            text("Select all applicable differences in the filtered results", "勾选筛选结果中全部可处理的差异项")),
                            button, GUILayout.Height(26f), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true)))
                            SelectVisible(visible);
                        GUILayout.Space(4f);
                        using (new EditorGUI.DisabledScope(_selected.Count == 0))
                        {
                            if (GUILayout.Button(new GUIContent(text("Clear", "清空"), text("Clear all checks, including hidden ones.",
                                "取消全部勾选，包括筛选外的勾选项。")), button, GUILayout.Height(26f), GUILayout.Width(64f)))
                            { _selected.Clear(); _repaint(); GUIUtility.ExitGUI(); }
                        }
                    }
                }
                int hidden = HiddenSelectionCount(visible);
                if (hidden > 0)
                    GUILayout.Label(new GUIContent(text("Includes ", "包含 ") + hidden + text(" hidden checks", " 项筛选外勾选"),
                        text("Apply includes these hidden checks. Clear removes all checks.", "应用时会一并处理这些隐藏勾选；“清空”可取消全部勾选。")),
                        WarningLabel(), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                GUILayout.Space(7f);
                if (visible.Count == 0)
                    GUILayout.Label(_selectionOnly && _selectionKeys.Count == 0
                        ? text("Select properties in Animation, then rescan.", "请在 Animation 选中属性后重新扫描。")
                        : text("No results. Clear search or turn off Differences.", "没有结果，可清空搜索或关闭“只看差异”。"), Label(false, true, true),
                        GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
                else DrawGroups(visible, text);
            }
            if (!string.IsNullOrEmpty(_notice)) EditorGUILayout.HelpBox(Message(_notice, text), MessageType.Info);
            if (_modifiedCount.HasValue)
                EditorGUILayout.HelpBox(text("Moved ", "已移动 ") + _modifiedCount.Value +
                    text(" curves. Rescanned.", " 条曲线，已重新扫描。"), MessageType.Info);
            GUILayout.Space(10f);
        }

        private void DrawSummary(Func<string, string, string> text)
        {
            int differences = 0;
            int unavailable = 0;
            int total = 0;
            foreach (URflowKeyframeMoveScanRow row in _preview.Rows)
            {
                if (_selectionOnly && !_selectionKeys.Contains(URflowKeyframeMoveService.BindingKey(row.Binding))) continue;
                total++;
                if (row.HasDifference) differences++;
                if (!string.IsNullOrEmpty(row.Error) || row.Result == null) unavailable++;
            }
            string summary = differences + text(" differences / ", " 项差异 / ") + total + text(" properties", " 项属性");
            if (unavailable > 0) summary += "  ·  " + unavailable + text(" unavailable", " 项不可处理");
            GUILayout.Label(summary, Label(false, true, true), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
        }

        private List<URflowKeyframeMoveScanRow> VisibleRows(Func<string, string, string> text)
        {
            var rows = new List<URflowKeyframeMoveScanRow>();
            if (_preview == null) return rows;
            string query = (_search ?? string.Empty).Trim();
            foreach (URflowKeyframeMoveScanRow row in _preview.Rows)
            {
                if (_selectionOnly && !_selectionKeys.Contains(URflowKeyframeMoveService.BindingKey(row.Binding))) continue;
                // Always retain errors in the difference view, even if the default could not be read.
                if (_differencesOnly && !row.HasDifference && string.IsNullOrEmpty(row.Error)) continue;
                string haystack = (row.Label ?? "") + " " + row.Binding.path + " " +
                    row.Binding.propertyName + " " + PropertyLabel(row.Binding, text);
                if (query.Length > 0 && haystack.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                rows.Add(row);
            }
            return rows;
        }

        private int HiddenSelectionCount(List<URflowKeyframeMoveScanRow> visible)
        {
            int shown = 0;
            foreach (URflowKeyframeMoveScanRow row in visible)
                if (_selected.Contains(row)) shown++;
            return _selected.Count - shown;
        }

        private void SelectVisible(List<URflowKeyframeMoveScanRow> visible)
        {
            foreach (URflowKeyframeMoveScanRow row in visible)
                if (row.CanApply) _selected.Add(row);
            _repaint();
        }

        private void DrawGroups(List<URflowKeyframeMoveScanRow> visible, Func<string, string, string> text)
        {
            var groups = new SortedDictionary<string, List<URflowKeyframeMoveScanRow>>(StringComparer.Ordinal);
            foreach (URflowKeyframeMoveScanRow row in visible)
            {
                string path = row.Binding.path ?? string.Empty;
                List<URflowKeyframeMoveScanRow> rows;
                if (!groups.TryGetValue(path, out rows))
                { rows = new List<URflowKeyframeMoveScanRow>(); groups.Add(path, rows); }
                rows.Add(row);
            }
            GUIStyle card = new GUIStyle();
            URflowVisuals.StyleButton(card);
            card.padding = new RectOffset(8, 8, 7, 7);
            foreach (var group in groups)
            {
                using (new EditorGUILayout.VerticalScope(card))
                {
                    DrawGroupHeader(group.Key, group.Value, text);
                    DrawGroupNotices(group.Value, text);
                    GUILayout.Space(3f);
                    DrawColumns(text);
                    using (new EditorGUI.DisabledScope(_pending || !_preview.IsValid))
                    {
                        foreach (URflowKeyframeMoveScanRow row in group.Value) DrawRow(row, text);
                    }
                }
                GUILayout.Space(6f);
            }
        }

        private void DrawGroupHeader(string path, List<URflowKeyframeMoveScanRow> rows, Func<string, string, string> text)
        {
            int eligible = 0;
            int selected = 0;
            foreach (URflowKeyframeMoveScanRow row in rows)
                if (row.CanApply) { eligible++; if (_selected.Contains(row)) selected++; }
            Rect line = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            bool mixed = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = selected > 0 && selected < eligible;
                using (new EditorGUI.DisabledScope(_pending || !_preview.IsValid || eligible == 0))
                {
                    bool check = EditorGUI.Toggle(new Rect(line.x, line.y + 2f, 18f, 18f), eligible > 0 && selected == eligible);
                    if (check != (eligible > 0 && selected == eligible))
                    {
                        foreach (URflowKeyframeMoveScanRow row in rows)
                        {
                            if (!row.CanApply) continue;
                            if (check) _selected.Add(row); else _selected.Remove(row);
                        }
                        _repaint();
                    }
                }
            }
            finally { EditorGUI.showMixedValue = mixed; }
            string title = path.Length == 0 ? text("Root", "根对象") : path.Substring(path.LastIndexOf('/') + 1);
            GUI.Label(new Rect(line.x + 22f, line.y, Mathf.Max(0f, line.width - 70f), 22f),
                new GUIContent(title, path.Length == 0 ? (_preview.Root != null ? _preview.Root.name : title) : path), Label(true));
            GUIStyle count = Label(false, false, true);
            count.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(line.xMax - 46f, line.y, 46f, 22f), rows.Count + text(" props", " 项"), count);
        }

        private void DrawColumns(Func<string, string, string> text)
        {
            Rect line = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
            float labelWidth = Mathf.Min(84f, line.width * .34f);
            float valueWidth = Mathf.Max(0f, (line.width - labelWidth) / 3f);
            GUIStyle label = SectionLabel();
            label.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(line.x + labelWidth, line.y, valueWidth, 16f),
                _anchor == URflowKeyframeMoveAnchor.First ? text("FIRST", "首帧") : text("LAST", "尾帧"), label);
            GUI.Label(new Rect(line.x + labelWidth + valueWidth, line.y, valueWidth, 16f), text("STATIC", "默认"), label);
            GUI.Label(new Rect(line.x + labelWidth + valueWidth * 2f, line.y, valueWidth, 16f), text("DIFF", "差值"), label);
        }

        private void DrawRow(URflowKeyframeMoveScanRow row, Func<string, string, string> text)
        {
            Rect line = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            float labelWidth = Mathf.Min(84f, line.width * .34f);
            float valueWidth = Mathf.Max(0f, (line.width - labelWidth) / 3f);
            using (new EditorGUI.DisabledScope(!row.CanApply))
            {
                bool wasChecked = _selected.Contains(row);
                bool check = EditorGUI.Toggle(new Rect(line.x, line.y + 3f, 18f, 18f), wasChecked);
                if (check != wasChecked)
                {
                    if (check) _selected.Add(row); else _selected.Remove(row);
                    _repaint();
                }
            }
            GUI.Label(new Rect(line.x + 22f, line.y, Mathf.Max(0f, labelWidth - 24f), 24f),
                new GUIContent(PropertyLabel(row.Binding, text), PropertyTooltip(row, text)), Label());
            DrawNumeric(new Rect(line.x + labelWidth, line.y, valueWidth, 24f), row.AnchorValue);
            DrawNumeric(new Rect(line.x + labelWidth + valueWidth, line.y, valueWidth, 24f), row.StaticValue);
            GUIStyle deltaStyle = NumericLabel();
            if (row.HasDifference) deltaStyle.normal.textColor = URflowVisuals.Amber;
            string delta = row.Delta.HasValue ? SignedNumber(row.Delta.Value) : "—";
            string exactDelta = row.Delta.HasValue ? row.Delta.Value.ToString("R", CultureInfo.InvariantCulture) : "";
            if (_mode == URflowKeyframeMoveMode.Proportional && row.Result != null && row.HasDifference)
                exactDelta += "\n" + text("Factor: ", "倍率：") + row.Result.DeltaOrFactor.ToString("G6", CultureInfo.InvariantCulture);
            GUI.Label(new Rect(line.x + labelWidth + valueWidth * 2f, line.y, valueWidth, 24f),
                new GUIContent(delta, exactDelta), deltaStyle);
            if (!string.IsNullOrEmpty(row.Error))
                GUILayout.Label(new GUIContent(ShortRowMessage(row.Error, text), Message(row.Error, text)), WarningLabel(),
                    GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
            if (row.Result != null && row.Result.IsZeroFactor)
                GUILayout.Label(new GUIContent(text("Factor 0 · removes motion", "倍率 0 · 动画变化归零"),
                    text("All keyframe values become zero; this property loses all animated motion.", "所有关键帧数值归零，该属性的动画变化会消失。")),
                    WarningLabel(), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
            else if (row.Result != null && row.Result.IsNegativeFactor)
                GUILayout.Label(new GUIContent(text("Negative factor · reverses motion", "负倍率 · 运动反转"),
                    text("A negative factor reverses both value signs and motion direction.", "负倍率会反转数值符号及运动方向。")),
                    WarningLabel(), GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
        }

        private static string PropertyTooltip(URflowKeyframeMoveScanRow row, Func<string, string, string> text)
        {
            string tooltip = row.Label + " / " + row.Binding.propertyName;
            if (row.Binding.propertyName.StartsWith("m_SizeDelta.", StringComparison.Ordinal))
                tooltip += "\n" + text("Size Delta is relative to the anchors. Stretched width/height also depend on the parent and anchors.",
                    "Size Delta 是相对锚点的尺寸差；拉伸后的实际宽高还取决于父级与锚点。");
            return tooltip;
        }

        private static string ShortRowMessage(string source, Func<string, string, string> text)
        {
            switch (source)
            {
                case "当前界面中找不到该动画属性对应的对象。":
                    return text("Object missing", "找不到对象");
                case "该对象的锚点或轴心有动画，无法按固定布局对齐位置或 Size Delta。":
                    return text("Animated anchors/pivot · cannot align", "锚点或轴心有动画，无法对齐");
                case "该属性由布局组件驱动，请先解除对应布局驱动再对齐。":
                case "该轴的锚点或轴心由布局组件驱动，请先解除对应布局驱动再对齐。":
                case "拉伸轴的位置或尺寸由布局组件驱动，请先解除对应布局驱动再对齐。":
                    return text("Layout driven · release before aligning", "布局驱动中，请先解除驱动");
                case "无法读取该属性的有效静态布局值。":
                    return text("Static value unavailable", "无法读取默认值");
                case "该属性没有可处理的关键帧。":
                    return text("No keyframes", "没有关键帧");
                case "拉伸布局的配对属性无法可靠读取或处理，请先修正该轴的位置或 Size Delta。":
                    return text("Paired position/size unavailable", "同轴位置或尺寸无法处理");
                case "Proportional adjustment cannot use a zero anchor for a nonzero default value.":
                    return text("Zero anchor · turn off Proportional", "基准帧为 0，请关闭“按比例”");
            }
            return Message(source, text);
        }

        private static void DrawGroupNotices(List<URflowKeyframeMoveScanRow> rows, Func<string, string, string> text)
        {
            var notices = new HashSet<string>(StringComparer.Ordinal);
            foreach (URflowKeyframeMoveScanRow row in rows)
            {
                if (string.IsNullOrEmpty(row.LayoutNotice) || !notices.Add(row.LayoutNotice)) continue;
                string summary = Message(row.LayoutNotice, text);
                if (row.LayoutNotice.StartsWith("X 拉伸布局：", StringComparison.Ordinal))
                    summary = text("Stretch X · check position + size if both differ", "X 拉伸 · 位置与尺寸都有差异时一起勾选");
                else if (row.LayoutNotice.StartsWith("Y 拉伸布局：", StringComparison.Ordinal))
                    summary = text("Stretch Y · check position + size if both differ", "Y 拉伸 · 位置与尺寸都有差异时一起勾选");
                GUILayout.Label(new GUIContent(summary, Message(row.LayoutNotice, text)), WarningLabel(),
                    GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
            }
        }

        private static void DrawNumeric(Rect rect, float? value)
        {
            string display = value.HasValue ? value.Value.ToString("G6", CultureInfo.InvariantCulture) : "—";
            string exact = value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "";
            GUI.Label(rect, new GUIContent(display, exact), NumericLabel());
        }

        private static string SignedNumber(double value)
        { return (value > 0d ? "+" : "") + value.ToString("G6", CultureInfo.InvariantCulture); }

        private static string Message(string source, Func<string, string, string> text)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (source.IndexOf('\n') >= 0)
            {
                string[] lines = source.Split('\n');
                for (int i = 0; i < lines.Length; i++) lines[i] = Message(lines[i].TrimEnd('\r'), text);
                return string.Join("\n", lines);
            }
            switch (source)
            {
                case "当前界面中找不到该动画属性对应的对象。":
                    return text("The animated object could not be found in the current UI.", source);
                case "仅支持 Transform / RectTransform 的位置、缩放与 RectTransform Size Delta 属性，请取消选择其他属性。":
                    return text("Only Transform/RectTransform position, scale and RectTransform Size Delta are supported. Deselect other properties.", source);
                case "该对象的锚点或轴心有动画，无法按固定布局对齐位置或 Size Delta。":
                    return text("This object's anchors or pivot are animated. Position and Size Delta cannot be aligned against a fixed layout.", source);
                case "该对象的锚点或轴心仍被其他动画预览驱动，无法可靠读取固定布局。":
                    return text("Another animation preview drives this object's anchors or pivot; its fixed layout cannot be read reliably.", source);
                case "RectTransform 的锚点或轴心包含无效数值，无法对齐布局。":
                    return text("RectTransform anchors or pivot contain invalid values; layout alignment is unavailable.", source);
                case "该轴的锚点或轴心由布局组件驱动，请先解除对应布局驱动再对齐。":
                    return text("A layout component drives this axis's anchors or pivot. Remove that layout drive before aligning.", source);
                case "拉伸轴的位置或尺寸由布局组件驱动，请先解除对应布局驱动再对齐。":
                    return text("A layout component drives the stretched axis's position or size. Remove that layout drive before aligning.", source);
                case "RectTransform 的锚点或轴心已经改变，请重新扫描或刷新预览。":
                    return text("RectTransform anchors or pivot have changed. Rescan before applying.", source);
                case "拉伸布局的配对属性无法可靠读取或处理，请先修正该轴的位置或 Size Delta。":
                    return text("The stretched layout's paired property cannot be read or processed reliably. Correct this axis's position or Size Delta first.", source);
                case "拉伸布局的同轴位置与尺寸需要一起对齐。请重新扫描，并同时勾选该轴的位置和 Size Delta 差异项。":
                    return text("Align the stretched axis's position and size together. Rescan, then check both its position and Size Delta differences.", source);
                case "X 拉伸布局：Left / Right 由 UI 位置 X 和 Size Delta X 共同决定；两项有差异时请一起勾选。":
                    return text("Stretched X: Left/Right depend on UI Position X and Size Delta X. Check both when both differ.", source);
                case "Y 拉伸布局：Top / Bottom 由 UI 位置 Y 和 Size Delta Y 共同决定；两项有差异时请一起勾选。":
                    return text("Stretched Y: Top/Bottom depend on UI Position Y and Size Delta Y. Check both when both differ.", source);
                case "该属性仍被其他动画预览驱动，无法可靠读取静态布局值。":
                    return text("Another animation preview still drives this property; its static value cannot be read reliably.", source);
                case "该属性由布局组件驱动，请先解除对应布局驱动再对齐。":
                    return text("A layout component drives this property. Remove that layout drive before aligning it.", source);
                case "无法读取该属性的有效静态布局值。":
                    return text("This property's static layout value could not be read.", source);
                case "该属性没有可处理的关键帧。":
                    return text("This property has no keyframes to process.", source);
                case "当前动画片段没有可扫描的位置或缩放属性。":
                case "当前动画片段没有可扫描的位置、缩放或 Size Delta 属性。":
                    return text("The current clip has no position, scale or Size Delta curves to scan.", source);
                case "不支持该批量调整模式。":
                    return text("This batch adjustment mode is not supported.", source);
                case "不支持该对齐基准。":
                    return text("This alignment anchor is not supported.", source);
                case "请先扫描当前片段，再勾选要对齐的差异属性。":
                    return text("Scan the current clip, then check the differences to align.", source);
                case "请勾选至少一项可处理的差异属性。":
                    return text("Check at least one applicable difference.", source);
                case "勾选项目不属于当前扫描或重复，请重新扫描。":
                    return text("A checked row is duplicated or belongs to another scan. Rescan before applying.", source);
                case "勾选项目包含已一致或无法处理的属性，请重新扫描。":
                    return text("A checked property is already aligned or unavailable. Rescan before applying.", source);
                case "勾选属性已改变，请重新扫描。":
                case "扫描候选列表已改变，请重新扫描。":
                    return text("The scanned properties have changed. Rescan before applying.", source);
                case "勾选的动画曲线已改变，请重新扫描后再应用。":
                    return text("A checked animation curve has changed. Rescan before applying.", source);
                case "勾选属性的静态布局值已改变，请重新扫描。":
                    return text("A checked static layout value has changed. Rescan before applying.", source);
                case "扫描预览结果已改变，请重新扫描。":
                    return text("The scan preview has changed. Rescan before applying.", source);
                case "当前对象、动画片段或位置/缩放属性集合已改变，请重新扫描。":
                case "当前对象、动画片段或位置/缩放/Size Delta 属性集合已改变，请重新扫描。":
                    return text("The object, animation clip, or position/scale curves have changed. Rescan before applying.", source);
                case "Animation 窗口的对象、动画或属性选择已经改变，请重新刷新。":
                    return text("The animation context has changed. Rescan before applying.", source);
                case "Animation 属性选择已改变，请重新扫描。":
                    return text("The Animation property selection has changed. Rescan before applying.", source);
                case "Animation 窗口没有当前动画片段。":
                    return text("The Animation window has no active clip.", source);
                case "请打开 Animation 窗口并选中要对齐的属性。":
                case "请打开 Animation 窗口并关联要扫描的界面和动画片段。":
                    return text("Open the Animation window and associate the current UI with an animation clip.",
                        "请打开 Animation 窗口并关联当前界面的动画片段。");
                case "检测到多个 Animation 窗口，请先只保留本次使用的一个窗口。":
                    return text("Keep only the Animation window used for this scan open.", source);
                case "原 Animation 窗口已经关闭，请重新刷新预览。":
                    return text("The original Animation window is closed. Reopen it and rescan.", source);
                case "请在 Animation 窗口关联当前界面的场景实例；无法用其他选中对象猜测绑定根。":
                    return text("Associate the current UI scene instance in the Animation window before scanning.", source);
                case "当前动画不是可编辑的 .anim 资源。":
                    return text("The active clip is not an editable .anim asset.", source);
                case "无法暂停 Animation 窗口预览，因此未读取默认值或写入动画。":
                    return text("Animation preview could not be paused; static values and animation were left unchanged.", source);
                case "已关闭 Animation 录制，避免对齐操作自动产生新关键帧。":
                    return text("Animation recording was turned off to prevent alignment from adding keyframes.", source);
                case "Animation 预览未能恢复，可在 Animation 窗口重新打开。":
                    return text("Animation preview could not be restored. Re-enable it in the Animation window.", source);
                case "请退出运行模式后再对齐界面动画。":
                    return text("Exit Play mode before aligning UI animations.", source);
                case "Unity 正在编译或导入资源，请稍后再试。":
                    return text("Unity is compiling or importing assets. Try again when it finishes.", source);
                case "Proportional adjustment cannot use a zero anchor for a nonzero default value.":
                    return text(source, "基准帧为 0、默认值非 0 时无法按比例调整，可取消比例选项使用整体平移。");
                case "The selected curve has no keyframes.":
                    return text(source, "该属性没有关键帧。");
                case "The static default value must be finite.":
                    return text(source, "默认值必须是有效的有限数值。");
            }
            const string applyPrefix = "无法应用勾选属性：";
            if (source.StartsWith(applyPrefix, StringComparison.Ordinal))
                return text("Unable to apply this property: ", applyPrefix) + Message(source.Substring(applyPrefix.Length), text);
            const string previewPrefix = "Animation 预览未能恢复：";
            if (source.StartsWith(previewPrefix, StringComparison.Ordinal))
                return text("Animation preview could not be restored: ", previewPrefix) + source.Substring(previewPrefix.Length);
            return source;
        }

        private static GUIStyle NumericLabel()
        {
            GUIStyle style = Label();
            style.fontSize = 10;
            style.alignment = TextAnchor.MiddleRight;
            return style;
        }

        private static GUIStyle Label(bool bold = false, bool wrap = false, bool muted = false)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = wrap, clipping = TextClipping.Clip
            };
            style.normal.textColor = muted ? new Color(.56f, .58f, .62f) : new Color(.87f, .88f, .9f);
            return style;
        }

        private static GUIStyle SectionLabel()
        { GUIStyle style = Label(false, false, true); style.fontSize = 9; return style; }

        private static GUIStyle WarningLabel()
        { GUIStyle style = Label(false, true); style.normal.textColor = URflowVisuals.Amber; return style; }

        private static string PropertyLabel(EditorCurveBinding binding, Func<string, string, string> text)
        {
            string name = binding.propertyName ?? "";
            string axis = name.Length > 0 ? name.Substring(name.LastIndexOf('.') + 1).ToUpperInvariant() : "";
            if (name.StartsWith("m_LocalScale.", StringComparison.Ordinal)) return text("Scale ", "缩放 ") + axis;
            if (name.StartsWith("m_AnchoredPosition.", StringComparison.Ordinal)) return text("UI Pos ", "UI 位置 ") + axis;
            if (name.StartsWith("m_SizeDelta.", StringComparison.Ordinal)) return text("Size Δ", "尺寸差 ") + axis;
            if (name.StartsWith("m_LocalPosition.", StringComparison.Ordinal)) return text("Pos ", "位置 ") + axis;
            return name;
        }

        public void Dispose()
        {
            _disposed = true;
            CancelPending();
            _selected.Clear();
        }

        internal void CancelPending()
        {
            EditorApplication.delayCall -= ReadPreview;
            EditorApplication.delayCall -= ReadSelection;
            if (_applyCallback != null) EditorApplication.delayCall -= _applyCallback;
            _applyCallback = null;
            _pending = false;
        }
    }
}
