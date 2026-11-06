using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    public sealed class URflowKeyframeMoveScanPreview
    {
        private readonly List<URflowKeyframeMoveScanRow> _rows = new List<URflowKeyframeMoveScanRow>();
        public AnimationClip Clip { get; internal set; }
        public GameObject Root { get; internal set; }
        public URflowKeyframeMoveAnchor Anchor { get; internal set; }
        public URflowKeyframeMoveMode Mode { get; internal set; }
        public IReadOnlyList<URflowKeyframeMoveScanRow> Rows { get; private set; }
        public bool IsValid { get; internal set; }
        public string Notice { get; internal set; }
        internal AnimationWindow Window;
        internal List<string> CandidateKeys;

        internal URflowKeyframeMoveScanPreview()
        {
            Rows = new ReadOnlyCollection<URflowKeyframeMoveScanRow>(_rows);
            Notice = string.Empty;
        }

        internal void Add(URflowKeyframeMoveScanRow row) { _rows.Add(row); }
        internal bool Contains(URflowKeyframeMoveScanRow row) { return _rows.Contains(row); }
    }

    public sealed class URflowKeyframeMoveScanRow
    {
        public EditorCurveBinding Binding { get; internal set; }
        public string Label { get; internal set; }
        public string Error { get; internal set; }
        public float? AnchorValue { get; internal set; }
        public float? StaticValue { get; internal set; }
        public double? Delta
        {
            get { return AnchorValue.HasValue && StaticValue.HasValue
                ? (double?)((double)StaticValue.Value - AnchorValue.Value) : null; }
        }
        public URflowKeyframeMoveMathResult Result { get; internal set; }
        public bool HasDifference
        {
            get { return AnchorValue.HasValue && StaticValue.HasValue &&
                !URflowKeyframeMoveScanService.ValuesAligned(Binding, AnchorValue.Value, StaticValue.Value); }
        }
        public bool CanApply
        {
            get { return HasDifference && Result != null && string.IsNullOrEmpty(Error); }
        }
        internal AnimationCurve SourceCurve;
        internal float StaticSnapshot;
    }

    /// <summary>
    /// Explicit current-clip scan. It owns the candidate list and never depends
    /// on Animation Window's property selection. Scan is read-only; apply is
    /// limited to caller-checked rows from this exact scan.
    /// </summary>
    public static class URflowKeyframeMoveScanService
    {
        public static bool TryCreatePreview(URflowKeyframeMoveMode mode,
            out URflowKeyframeMoveScanPreview preview, out string error)
        {
            return TryCreatePreview(URflowKeyframeMoveAnchor.Last, mode, out preview, out error);
        }

        public static bool TryCreatePreview(URflowKeyframeMoveAnchor anchor, URflowKeyframeMoveMode mode,
            out URflowKeyframeMoveScanPreview preview, out string error)
        {
            preview = null;
            error = null;
            if (!URflowKeyframeMoveService.CanOperate(out error)) return false;
            if (anchor != URflowKeyframeMoveAnchor.First && anchor != URflowKeyframeMoveAnchor.Last)
            {
                error = "不支持该对齐基准。";
                return false;
            }
            if (mode != URflowKeyframeMoveMode.Translate && mode != URflowKeyframeMoveMode.Proportional)
            {
                error = "不支持该批量调整模式。";
                return false;
            }
            try
            {
                URflowKeyframeMoveService.Context context = URflowKeyframeMoveService.CaptureContext(null, true);
                preview = new URflowKeyframeMoveScanPreview
                {
                    Clip = context.Clip, Root = context.Root, Anchor = anchor, Mode = mode,
                    Window = context.Window,
                    CandidateKeys = URflowKeyframeMoveService.BindingKeys(context.Bindings)
                };
                if (context.Bindings.Count == 0)
                {
                    error = "当前动画片段没有可扫描的位置或缩放属性。";
                    return false;
                }
                URflowKeyframeMoveScanPreview captured = preview;
                string notice = string.Empty;
                try
                {
                    URflowKeyframeMoveService.OutsidePreview(context, delegate
                    {
                        URflowKeyframeMoveService.ValidateContext(context);
                        foreach (EditorCurveBinding binding in context.Bindings)
                        {
                            var row = new URflowKeyframeMoveScanRow
                            {
                                Binding = binding,
                                Label = (string.IsNullOrEmpty(binding.path) ? "(Root)" : binding.path) +
                                    " / " + binding.propertyName,
                                Error = string.Empty
                            };
                            captured.Add(row);
                            AnimationCurve curve = AnimationUtility.GetEditorCurve(context.Clip, binding);
                            if (curve == null || curve.length == 0)
                            {
                                row.Error = "该属性没有可处理的关键帧。";
                                continue;
                            }
                            row.SourceCurve = URflowKeyframeMoveService.CloneCurve(curve);
                            row.AnchorValue = curve.keys[anchor == URflowKeyframeMoveAnchor.First ? 0 : curve.length - 1].value;
                            float value;
                            string readError;
                            if (!URflowKeyframeMoveService.TryReadStaticValue(context.Root, binding, out value, out readError))
                            {
                                row.Error = readError;
                                continue;
                            }
                            row.StaticValue = value;
                            row.StaticSnapshot = value;
                            URflowKeyframeMoveMathResult result;
                            string mathError;
                            // A within-allowance endpoint is a no-op, including a near-zero
                            // proportional anchor. Do not manufacture an error for float noise.
                            float mathTarget = row.HasDifference ? value : row.AnchorValue.Value;
                            if (!URflowKeyframeMoveMath.TryBuildCurve(curve, mathTarget,
                                anchor, mode, out result, out mathError))
                            {
                                row.Error = mathError;
                                continue;
                            }
                            row.Result = result;
                        }
                    }, out notice);
                }
                finally { captured.Notice = notice; }
                // Invalid rows are still a valid scan result. Only checked,
                // individually eligible rows are ever allowed to be written.
                captured.IsValid = true;
                return true;
            }
            catch (Exception exception)
            {
                URflowKeyframeMoveService.RethrowExitGUI(exception);
                if (preview != null) preview.IsValid = false;
                error = URflowKeyframeMoveService.FriendlyError(exception);
                return false;
            }
        }

        public static bool TryApply(URflowKeyframeMoveScanPreview preview,
            IEnumerable<URflowKeyframeMoveScanRow> selectedRows,
            out int modifiedCurveCount, out string error)
        {
            modifiedCurveCount = 0;
            error = null;
            if (!URflowKeyframeMoveService.CanOperate(out error)) return false;
            if (preview == null || !preview.IsValid || selectedRows == null)
            {
                error = "请先扫描当前片段，再勾选要对齐的差异属性。";
                return false;
            }
            try
            {
                var checkedRows = new List<URflowKeyframeMoveScanRow>();
                var unique = new HashSet<URflowKeyframeMoveScanRow>();
                foreach (URflowKeyframeMoveScanRow row in selectedRows)
                {
                    if (row == null || !preview.Contains(row) || !unique.Add(row))
                        throw new InvalidOperationException("勾选项目不属于当前扫描或重复，请重新扫描。");
                    if (!row.CanApply)
                        throw new InvalidOperationException("勾选项目包含已一致或无法处理的属性，请重新扫描。");
                    checkedRows.Add(row);
                }
                if (checkedRows.Count == 0)
                {
                    error = "请勾选至少一项可处理的差异属性。";
                    return false;
                }
                URflowKeyframeMoveService.Context context =
                    URflowKeyframeMoveService.CaptureContext(preview.Window, true);
                ValidatePreviewContext(context, preview);
                string notice = string.Empty;
                int changedCount = 0;
                try
                {
                    URflowKeyframeMoveService.OutsidePreview(context, delegate
                    {
                        URflowKeyframeMoveService.ValidateContext(context);
                        ValidatePreviewContext(context, preview);
                        var bindings = new List<EditorCurveBinding>();
                        var curves = new List<AnimationCurve>();
                        // Validate every checked row before the first write.
                        // Errors on unchecked rows never block eligible choices.
                        foreach (URflowKeyframeMoveScanRow row in checkedRows)
                        {
                            string unsupported;
                            if (!URflowKeyframeMoveService.IsSupportedBinding(row.Binding, out unsupported))
                                throw new InvalidOperationException("勾选属性已改变，请重新扫描。");
                            AnimationCurve current = AnimationUtility.GetEditorCurve(context.Clip, row.Binding);
                            if (!URflowKeyframeMoveService.CurvesEqual(current, row.SourceCurve))
                                throw new InvalidOperationException("勾选的动画曲线已改变，请重新扫描后再应用。");
                            float value;
                            string readError;
                            if (!URflowKeyframeMoveService.TryReadStaticValue(context.Root, row.Binding, out value, out readError))
                                throw new InvalidOperationException(readError);
                            if (!value.Equals(row.StaticSnapshot) || !row.StaticValue.HasValue ||
                                !row.StaticValue.Value.Equals(row.StaticSnapshot))
                                throw new InvalidOperationException("勾选属性的静态布局值已改变，请重新扫描。");
                            URflowKeyframeMoveMathResult rebuilt;
                            string mathError;
                            if (!URflowKeyframeMoveMath.TryBuildCurve(current, value,
                                preview.Anchor, preview.Mode, out rebuilt, out mathError))
                                throw new InvalidOperationException("无法应用勾选属性：" + mathError);
                            if (row.Result == null || row.Result.Anchor != preview.Anchor || row.Result.Mode != preview.Mode ||
                                !URflowKeyframeMoveService.CurvesEqual(rebuilt.Curve, row.Result.Curve))
                                throw new InvalidOperationException("扫描预览结果已改变，请重新扫描。");
                            if (!rebuilt.HasChanges) continue;
                            bindings.Add(row.Binding);
                            curves.Add(rebuilt.Curve);
                        }
                        if (bindings.Count == 0) return;
                        URflowKeyframeMoveService.WriteAtomically(context.Clip, bindings, curves);
                        changedCount = bindings.Count;
                    }, out notice);
                }
                finally { preview.Notice = notice; }
                modifiedCurveCount = changedCount;
                if (changedCount > 0) preview.IsValid = false;
                context.Window.Repaint();
                SceneView.RepaintAll();
                return true;
            }
            catch (Exception exception)
            {
                URflowKeyframeMoveService.RethrowExitGUI(exception);
                preview.IsValid = false;
                error = URflowKeyframeMoveService.FriendlyError(exception);
                return false;
            }
        }

        private static void ValidatePreviewContext(
            URflowKeyframeMoveService.Context context, URflowKeyframeMoveScanPreview preview)
        {
            if ((preview.Anchor != URflowKeyframeMoveAnchor.First && preview.Anchor != URflowKeyframeMoveAnchor.Last) ||
                (preview.Mode != URflowKeyframeMoveMode.Translate && preview.Mode != URflowKeyframeMoveMode.Proportional))
                throw new InvalidOperationException("扫描预览结果已改变，请重新扫描。");
            if (!context.ScanAll || context.Window != preview.Window || context.Clip != preview.Clip ||
                context.Root != preview.Root ||
                !KeysEqual(URflowKeyframeMoveService.BindingKeys(context.Bindings), preview.CandidateKeys))
                throw new InvalidOperationException("当前对象、动画片段或位置/缩放属性集合已改变，请重新扫描。");
            var bindings = new List<EditorCurveBinding>();
            foreach (URflowKeyframeMoveScanRow row in preview.Rows) bindings.Add(row.Binding);
            if (!KeysEqual(URflowKeyframeMoveService.BindingKeys(bindings), preview.CandidateKeys))
                throw new InvalidOperationException("扫描候选列表已改变，请重新扫描。");
        }

        // Filtering alone uses a small absolute allowance for float serialization/reading noise.
        // Stale snapshots and atomic write verification deliberately keep exact comparisons.
        internal static bool ValuesAligned(EditorCurveBinding binding, float anchor, float target)
        {
            if (float.IsNaN(anchor) || float.IsInfinity(anchor) || float.IsNaN(target) || float.IsInfinity(target))
                return false;
            double tolerance = (binding.propertyName ?? string.Empty).StartsWith("m_LocalScale.", StringComparison.Ordinal)
                ? 0.000001d : 0.0001d;
            return Math.Abs((double)target - anchor) <= tolerance;
        }

        // UI selection is an optional view filter. Empty selection must return no keys,
        // and reading it must never alter the full scan's application scope or validity.
        internal static bool TryGetSelectedBindingKeys(URflowKeyframeMoveScanPreview preview,
            out List<string> keys, out string error)
        {
            keys = new List<string>();
            error = null;
            if (!URflowKeyframeMoveService.CanOperate(out error)) return false;
            if (preview == null || !preview.IsValid)
            {
                error = "请先扫描当前片段，再筛选选中属性。";
                return false;
            }
            try
            {
                URflowKeyframeMoveService.Context context =
                    URflowKeyframeMoveService.CaptureContext(preview.Window, true);
                ValidatePreviewContext(context, preview);
                List<EditorCurveBinding> selected = URflowKeyframeMoveService.ReadSelectedBindings(
                    context.Window, context.State, context.Clip, true);
                var supported = new List<EditorCurveBinding>();
                foreach (EditorCurveBinding binding in selected)
                {
                    string unsupported;
                    if (URflowKeyframeMoveService.IsSupportedBinding(binding, out unsupported) &&
                        preview.CandidateKeys.Contains(URflowKeyframeMoveService.BindingKey(binding)))
                        supported.Add(binding);
                }
                keys = URflowKeyframeMoveService.BindingKeys(supported);
                return true;
            }
            catch (Exception exception)
            {
                URflowKeyframeMoveService.RethrowExitGUI(exception);
                error = URflowKeyframeMoveService.FriendlyError(exception);
                return false;
            }
        }

        private static bool KeysEqual(List<string> left, List<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
