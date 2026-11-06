using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace URflow
{
    public sealed class URflowKeyframeMovePreview
    {
        public AnimationClip Clip { get; internal set; }
        public GameObject Root { get; internal set; }
        public URflowKeyframeMoveAnchor Anchor { get; internal set; }
        public URflowKeyframeMoveMode Mode { get; internal set; }
        public List<URflowKeyframeMovePreviewRow> Rows { get; private set; }
        public bool IsValid { get; internal set; }
        public string Notice { get; internal set; }
        internal AnimationWindow Window;
        internal List<string> SelectionKeys;

        internal URflowKeyframeMovePreview()
        {
            Rows = new List<URflowKeyframeMovePreviewRow>();
            Notice = string.Empty;
        }
    }

    public sealed class URflowKeyframeMovePreviewRow
    {
        public EditorCurveBinding Binding { get; internal set; }
        public string Label { get; internal set; }
        public string Error { get; internal set; }
        public bool IsSupported { get; internal set; }
        public URflowKeyframeMoveMathResult Result { get; internal set; }
        internal AnimationCurve SourceCurve;
        internal float StaticValue;
    }

    /// <summary>
    /// Animation Window adapter. Call refresh/apply outside OnGUI, for example
    /// through EditorApplication.delayCall. No clip data is written by refresh.
    /// </summary>
    public static class URflowKeyframeMoveService
    {
        private const BindingFlags Members =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Shared adapter for the explicit selected-properties and batch-scan entries.
        // ScanAll never falls back from an empty property selection.
        internal sealed class Context
        {
            public AnimationWindow Window;
            public object State;
            public AnimationClip Clip;
            public GameObject Root;
            public List<EditorCurveBinding> Bindings;
            public bool ScanAll;
        }

        private sealed class ClipSnapshot
        {
            public readonly Dictionary<string, AnimationCurve> Curves =
                new Dictionary<string, AnimationCurve>();
            public readonly Dictionary<string, ObjectReferenceKeyframe[]> ObjectCurves =
                new Dictionary<string, ObjectReferenceKeyframe[]>();
        }

        public static bool TryCreatePreview(
            URflowKeyframeMoveAnchor anchor, URflowKeyframeMoveMode mode,
            out URflowKeyframeMovePreview preview, out string error)
        {
            preview = null;
            error = null;
            if (!CanOperate(out error)) return false;
            try
            {
                Context context = CaptureContext(null);
                var captured = new URflowKeyframeMovePreview
                {
                    Clip = context.Clip,
                    Root = context.Root,
                    Anchor = anchor,
                    Mode = mode,
                    Window = context.Window,
                    SelectionKeys = BindingKeys(context.Bindings)
                };
                preview = captured;
                bool valid = true;
                string notice = string.Empty;
                try
                {
                    OutsidePreview(context, delegate
                    {
                        ValidateContext(context);
                        foreach (EditorCurveBinding binding in context.Bindings)
                        {
                            var row = new URflowKeyframeMovePreviewRow
                            {
                                Binding = binding,
                                Label = MakeLabel(binding),
                                Error = string.Empty
                            };
                            captured.Rows.Add(row);
                            string unsupported;
                            row.IsSupported = IsSupportedBinding(binding, out unsupported);
                            if (!row.IsSupported)
                            {
                                row.Error = unsupported;
                                valid = false;
                                continue;
                            }
                            AnimationCurve curve = AnimationUtility.GetEditorCurve(context.Clip, binding);
                            if (curve == null || curve.length == 0)
                            {
                                row.Error = "该属性没有可处理的关键帧。";
                                valid = false;
                                continue;
                            }
                            row.SourceCurve = CloneCurve(curve);
                            float staticValue;
                            string readError;
                            if (!TryReadStaticValue(context.Root, binding, out staticValue, out readError))
                            {
                                row.Error = readError;
                                valid = false;
                                continue;
                            }
                            row.StaticValue = staticValue;
                            URflowKeyframeMoveMathResult result;
                            string mathError;
                            if (!URflowKeyframeMoveMath.TryBuildCurve(
                                curve, staticValue, anchor, mode, out result, out mathError))
                            {
                                row.Error = "无法生成该属性的对齐预览：" + mathError;
                                valid = false;
                                continue;
                            }
                            row.Result = result;
                        }
                    }, out notice);
                }
                finally
                {
                    captured.Notice = notice;
                }
                captured.IsValid = valid && captured.Rows.Count > 0;
                if (!captured.IsValid)
                {
                    error = "所选属性中有无法处理的项目，请查看预览提示并在 Animation 窗口调整选择。";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                RethrowExitGUI(exception);
                if (preview != null) preview.IsValid = false;
                error = FriendlyError(exception);
                return false;
            }
        }

        public static bool TryApply(
            URflowKeyframeMovePreview preview, out int modifiedCurveCount, out string error)
        {
            modifiedCurveCount = 0;
            error = null;
            if (!CanOperate(out error)) return false;
            if (preview == null || !preview.IsValid || preview.Rows.Count == 0)
            {
                error = "请先刷新并确认有效的对齐预览。";
                return false;
            }
            try
            {
                Context context = CaptureContext(preview.Window);
                ValidatePreviewContext(context, preview);
                int changedCount = 0;
                string notice = string.Empty;
                try
                {
                    OutsidePreview(context, delegate
                    {
                        ValidateContext(context);
                        ValidatePreviewContext(context, preview);
                        var bindings = new List<EditorCurveBinding>();
                        var curves = new List<AnimationCurve>();
                        foreach (URflowKeyframeMovePreviewRow row in preview.Rows)
                        {
                            string unsupported;
                            if (!IsSupportedBinding(row.Binding, out unsupported) || row.Result == null)
                                throw new InvalidOperationException("预览包含无法处理的属性，请重新刷新。");
                            AnimationCurve current = AnimationUtility.GetEditorCurve(context.Clip, row.Binding);
                            if (!CurvesEqual(current, row.SourceCurve))
                                throw new InvalidOperationException("动画曲线已经改变，请重新刷新预览后再应用。");
                            float value;
                            string readError;
                            if (!TryReadStaticValue(context.Root, row.Binding, out value, out readError))
                                throw new InvalidOperationException(readError);
                            if (!value.Equals(row.StaticValue))
                                throw new InvalidOperationException("界面静态布局值已经改变，请重新刷新预览。");
                            URflowKeyframeMoveMathResult rebuilt;
                            string mathError;
                            if (!URflowKeyframeMoveMath.TryBuildCurve(
                                current, value, preview.Anchor, preview.Mode, out rebuilt, out mathError))
                                throw new InvalidOperationException("无法应用该属性：" + mathError);
                            if (!CurvesEqual(rebuilt.Curve, row.Result.Curve))
                                throw new InvalidOperationException("预览结果已经改变，请重新刷新。");
                            if (!rebuilt.HasChanges) continue;
                            bindings.Add(row.Binding);
                            curves.Add(rebuilt.Curve);
                        }
                        if (bindings.Count == 0) return;
                        WriteAtomically(context.Clip, bindings, curves);
                        changedCount = bindings.Count;
                    }, out notice);
                }
                finally
                {
                    preview.Notice = notice;
                }
                modifiedCurveCount = changedCount;
                if (changedCount > 0) preview.IsValid = false;
                context.Window.Repaint();
                SceneView.RepaintAll();
                return true;
            }
            catch (Exception exception)
            {
                RethrowExitGUI(exception);
                preview.IsValid = false;
                error = FriendlyError(exception);
                return false;
            }
        }

        internal static bool CanOperate(out string error)
        {
            error = null;
            if (Event.current != null)
            {
                error = "请在 GUI 绘制结束后刷新或应用对齐结果。";
                return false;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "请退出运行模式后再对齐界面动画。";
                return false;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                error = "Unity 正在编译或导入资源，请稍后再试。";
                return false;
            }
            return true;
        }

        internal static Context CaptureContext(AnimationWindow expected, bool scanAll = false)
        {
            AnimationWindow[] windows = Resources.FindObjectsOfTypeAll<AnimationWindow>();
            AnimationWindow window = expected;
            if (window == null)
            {
                window = EditorWindow.focusedWindow as AnimationWindow;
                if (window == null)
                {
                    if (windows.Length != 1)
                        throw new InvalidOperationException(windows.Length == 0
                            ? (scanAll ? "请打开 Animation 窗口并关联要扫描的界面和动画片段。" : "请打开 Animation 窗口并选中要对齐的属性。")
                            : "检测到多个 Animation 窗口，请先只保留本次使用的一个窗口。");
                    window = windows[0];
                }
            }
            bool exists = false;
            foreach (AnimationWindow candidate in windows)
                if (candidate == window) exists = true;
            if (!exists)
                throw new InvalidOperationException("原 Animation 窗口已经关闭，请重新刷新预览。");
            object state = GetMember(window, "state");
            if (state == null)
            {
                object editor = GetMember(window, "m_AnimEditor");
                state = GetMember(editor, "state") ?? GetMember(editor, "m_State");
            }
            if (state == null)
                throw new InvalidOperationException("无法读取 Animation 窗口状态；该 Unity 版本的内部接口可能不兼容。");
            var clip = GetMember(state, "activeAnimationClip") as AnimationClip;
            var root = GetMember(state, "activeRootGameObject") as GameObject;
            if (clip == null)
                throw new InvalidOperationException("Animation 窗口没有当前动画片段。");
            if (root == null || EditorUtility.IsPersistent(root) || !root.scene.IsValid())
                throw new InvalidOperationException("请在 Animation 窗口关联当前界面的场景实例；无法用其他选中对象猜测绑定根。");
            string clipPath = AssetDatabase.GetAssetPath(clip);
            if ((clip.hideFlags & HideFlags.NotEditable) != 0 ||
                (!string.IsNullOrEmpty(clipPath) &&
                 (!clipPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                  !AssetDatabase.IsOpenForEdit(clipPath))))
                throw new InvalidOperationException("当前动画不是可编辑的 .anim 资源。");
            return new Context
            {
                Window = window, State = state, Clip = clip, Root = root,
                Bindings = scanAll ? ReadScanBindings(clip) : ReadSelectedBindings(window, state, clip),
                ScanAll = scanAll
            };
        }

        private static List<EditorCurveBinding> ReadScanBindings(AnimationClip clip)
        {
            var supported = new SortedDictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                string error;
                if (IsSupportedBinding(binding, out error)) AddBinding(supported, binding);
            }
            return new List<EditorCurveBinding>(supported.Values);
        }

        internal static List<EditorCurveBinding> ReadSelectedBindings(
            AnimationWindow window, object state, AnimationClip clip, bool allowEmpty = false)
        {
            var hierarchySelection = new SortedDictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            IList nodes = GetMember(state, "selectedHierarchyNodes") as IList;
            if (nodes != null)
            {
                foreach (object node in nodes)
                {
                    if (node == null) continue;
                    string name = node.GetType().Name;
                    if (name == "AnimationWindowHierarchyPropertyNode")
                    {
                        object binding = GetMember(node, "binding");
                        if (binding is EditorCurveBinding)
                            AddBinding(hierarchySelection, (EditorCurveBinding)binding);
                    }
                    else if (name == "AnimationWindowHierarchyPropertyGroupNode")
                    {
                        IEnumerable groupCurves = GetMember(node, "curves") as IEnumerable;
                        if (groupCurves == null)
                            throw new InvalidOperationException("所选属性组尚未加载，请在 Animation 窗口展开属性后重试。");
                        foreach (object curve in groupCurves)
                            AddWindowCurve(hierarchySelection, curve, clip);
                    }
                }
            }

            var keySelection = new SortedDictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            IEnumerable keys = GetMember(state, "selectedKeys") as IEnumerable;
            if (keys != null)
                foreach (object key in keys)
                    AddWindowCurve(keySelection,
                        GetMember(key, "curve") ?? GetMember(key, "m_curve") ?? GetMember(key, "m_Curve"),
                        clip);

            // CurveEditor selection is meaningful only while its view is active.
            // Hidden Curves-view caches must not affect a Dopesheet selection.
            object showCurveEditor = GetMember(state, "showCurveEditor");
            bool curvesView = showCurveEditor is bool && (bool)showCurveEditor;
            var editorSelection = new SortedDictionary<string, EditorCurveBinding>(StringComparer.Ordinal);
            if (curvesView)
            {
                object editor = GetMember(window, "m_AnimEditor");
                object curveEditor = GetMember(editor, "m_CurveEditor");
                IEnumerable curveSelections = GetMember(curveEditor, "selectedCurves") as IEnumerable;
                IEnumerable wrappers = GetMember(curveEditor, "animationCurves") as IEnumerable;
                var selectedIds = new HashSet<int>();
                if (curveSelections != null)
                    foreach (object selection in curveSelections)
                    {
                        object id = GetMember(selection, "curveID");
                        if (id is int) selectedIds.Add((int)id);
                    }
                if (wrappers != null)
                    foreach (object wrapper in wrappers)
                    {
                        object id = GetMember(wrapper, "id");
                        if (!(id is int) || !selectedIds.Contains((int)id)) continue;
                        var wrapperClip = GetMember(wrapper, "animationClip") as AnimationClip;
                        object binding = GetMember(wrapper, "binding");
                        if (wrapperClip != clip)
                            throw new InvalidOperationException("所选曲线不属于当前动画片段，请重新选择。");
                        if (binding is EditorCurveBinding)
                            AddBinding(editorSelection, (EditorCurveBinding)binding);
                    }
            }

            if (keySelection.Count > 0 && editorSelection.Count > 0 &&
                !SelectionMapsEqual(keySelection, editorSelection))
                throw new InvalidOperationException(
                    "Animation 关键帧选择不同步。请先点击时间轴空白处清除选择，再选择目标属性并刷新。");

            SortedDictionary<string, EditorCurveBinding> activeKeys =
                editorSelection.Count > 0 ? editorSelection : keySelection;
            if (hierarchySelection.Count > 0 && activeKeys.Count > 0 &&
                !SelectionMapsEqual(hierarchySelection, activeKeys))
                throw new InvalidOperationException(
                    "Animation 属性行与关键帧选择不一致。请先点击时间轴空白处清除关键帧选择，再选择目标属性并刷新。");

            SortedDictionary<string, EditorCurveBinding> selected =
                hierarchySelection.Count > 0 ? hierarchySelection : activeKeys;
            if (selected.Count == 0 && !allowEmpty)
                throw new InvalidOperationException("请在 Animation 窗口选中具体属性、属性组，或该属性的关键帧。不会自动处理整个动画片段。");
            return new List<EditorCurveBinding>(selected.Values);
        }

        private static bool SelectionMapsEqual(
            SortedDictionary<string, EditorCurveBinding> left,
            SortedDictionary<string, EditorCurveBinding> right)
        {
            return StringListsEqual(new List<string>(left.Keys), new List<string>(right.Keys));
        }

        private static void AddWindowCurve(
            SortedDictionary<string, EditorCurveBinding> selected, object curve, AnimationClip clip)
        {
            if (curve == null) return;
            object binding = GetMember(curve, "binding") ?? GetMember(curve, "m_Binding");
            var curveClip = (GetMember(curve, "clip") ?? GetMember(curve, "m_Clip")) as AnimationClip;
            if (curveClip != clip)
                throw new InvalidOperationException("所选属性不属于当前动画片段，请重新选择。");
            if (!(binding is EditorCurveBinding))
                throw new InvalidOperationException("无法识别所选属性绑定；该 Unity 版本的内部接口可能不兼容。");
            AddBinding(selected, (EditorCurveBinding)binding);
        }

        private static void AddBinding(
            SortedDictionary<string, EditorCurveBinding> selected, EditorCurveBinding binding)
        {
            selected[BindingKey(binding)] = binding;
        }

        internal static void ValidateContext(Context captured)
        {
            Context current = CaptureContext(captured.Window, captured.ScanAll);
            if (current.Clip != captured.Clip || current.Root != captured.Root ||
                !StringListsEqual(BindingKeys(current.Bindings), BindingKeys(captured.Bindings)))
                throw new InvalidOperationException("Animation 窗口的对象、动画或属性选择已经改变，请重新刷新。");
        }

        private static void ValidatePreviewContext(
            Context context, URflowKeyframeMovePreview preview)
        {
            if (context.Clip != preview.Clip || context.Root != preview.Root ||
                context.Window != preview.Window ||
                !StringListsEqual(BindingKeys(context.Bindings), preview.SelectionKeys) ||
                preview.Rows.Count != context.Bindings.Count)
                throw new InvalidOperationException("Animation 窗口的对象、动画或属性选择已经改变，请重新刷新。");
            var rowKeys = new List<string>();
            foreach (URflowKeyframeMovePreviewRow row in preview.Rows)
                rowKeys.Add(BindingKey(row.Binding));
            rowKeys.Sort(StringComparer.Ordinal);
            if (!StringListsEqual(rowKeys, preview.SelectionKeys))
                throw new InvalidOperationException("预览中的属性选择已经改变，请重新刷新。");
        }

        internal static void OutsidePreview(Context context, Action action, out string notice)
        {
            notice = string.Empty;
            bool wasPreviewing = context.Window.previewing;
            bool wasRecording = context.Window.recording;
            float time = context.Window.time;
            try
            {
                context.Window.recording = false;
                context.Window.previewing = false;
                if (context.Window.previewing || context.Window.recording)
                    throw new InvalidOperationException("无法暂停 Animation 窗口预览，因此未读取默认值或写入动画。");
                action();
            }
            finally
            {
                if (wasRecording)
                    notice = "已关闭 Animation 录制，避免对齐操作自动产生新关键帧。";
                if (wasPreviewing && context.Window != null)
                {
                    try
                    {
                        context.Window.previewing = true;
                        context.Window.time = time;
                        context.Window.Repaint();
                        if (!context.Window.previewing)
                            notice = AppendNotice(notice, "Animation 预览未能恢复，可在 Animation 窗口重新打开。");
                    }
                    catch (Exception exception)
                    {
                        RethrowExitGUI(exception);
                        notice = AppendNotice(notice, "Animation 预览未能恢复：" + FriendlyError(exception));
                    }
                }
            }
        }

        internal static bool IsSupportedBinding(EditorCurveBinding binding, out string error)
        {
            error = null;
            bool transform = binding.type == typeof(Transform) || binding.type == typeof(RectTransform);
            string property = binding.propertyName ?? string.Empty;
            bool positionOrScale = IsAxis(property, "m_LocalPosition") || IsAxis(property, "m_LocalScale");
            bool anchored = binding.type == typeof(RectTransform) &&
                (property == "m_AnchoredPosition.x" || property == "m_AnchoredPosition.y");
            if (binding.isPPtrCurve || binding.isDiscreteCurve ||
                !transform || (!positionOrScale && !anchored))
            {
                error = "首版仅支持 Transform / RectTransform 的位置与缩放属性，请取消选择其他属性。";
                return false;
            }
            return true;
        }

        internal static bool TryReadStaticValue(
            GameObject root, EditorCurveBinding binding, out float value, out string error)
        {
            value = 0f;
            error = null;
            Object target = AnimationUtility.GetAnimatedObject(root, binding);
            if (target == null)
            {
                error = "当前界面中找不到该动画属性对应的对象。";
                return false;
            }
            if (AnimationMode.IsPropertyAnimated(target, binding.propertyName))
            {
                error = "该属性仍被其他动画预览驱动，无法可靠读取静态布局值。";
                return false;
            }
            var rect = target as RectTransform;
            if (rect != null && IsDriven(rect, binding.propertyName))
            {
                error = "该属性由布局组件驱动，请先解除对应布局驱动再对齐。";
                return false;
            }
            if (!AnimationUtility.GetFloatValue(root, binding, out value) ||
                float.IsNaN(value) || float.IsInfinity(value))
            {
                error = "无法读取该属性的有效静态布局值。";
                return false;
            }
            return true;
        }

        private static bool IsDriven(RectTransform rect, string property)
        {
            if (rect.drivenByObject == null) return false;
            DrivenTransformProperties flag;
            if (property == "m_AnchoredPosition.x" || property == "m_LocalPosition.x")
                flag = DrivenTransformProperties.AnchoredPositionX;
            else if (property == "m_AnchoredPosition.y" || property == "m_LocalPosition.y")
                flag = DrivenTransformProperties.AnchoredPositionY;
            else if (property == "m_LocalPosition.z")
                flag = DrivenTransformProperties.AnchoredPositionZ;
            else if (property == "m_LocalScale.x")
                flag = DrivenTransformProperties.ScaleX;
            else if (property == "m_LocalScale.y")
                flag = DrivenTransformProperties.ScaleY;
            else if (property == "m_LocalScale.z")
                flag = DrivenTransformProperties.ScaleZ;
            else return false;
            // Unity 2022.3 exposes drivenByObject publicly, but the per-axis
            // drivenProperties getter is internal. Fail closed if unavailable.
            object driven = GetMember(rect, "drivenProperties");
            return !(driven is DrivenTransformProperties) ||
                (((DrivenTransformProperties)driven & flag) != 0);
        }

        internal static void WriteAtomically(
            AnimationClip clip, List<EditorCurveBinding> bindings, List<AnimationCurve> curves)
        {
            ClipSnapshot before = CaptureClip(clip);
            var expected = new Dictionary<string, AnimationCurve>(before.Curves);
            for (int i = 0; i < bindings.Count; i++)
                expected[BindingKey(bindings[i])] = curves[i];
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            const string operation = "URflow 关键帧移动";
            Undo.SetCurrentGroupName(operation);
            Undo.RegisterCompleteObjectUndo(clip, operation);
            try
            {
                AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
                ClipSnapshot stored = CaptureClip(clip);
                if (stored.Curves.Count != expected.Count)
                    throw new InvalidOperationException("Unity 改变了未选属性的曲线结构，本次操作已撤销。");
                foreach (var entry in expected)
                {
                    AnimationCurve actual;
                    if (!stored.Curves.TryGetValue(entry.Key, out actual) ||
                        !CurvesEqual(actual, entry.Value))
                        throw new InvalidOperationException("Unity 保存后的曲线或切线与预览不一致，本次操作已撤销。");
                }
                if (!ObjectCurvesEqual(before.ObjectCurves, stored.ObjectCurves))
                    throw new InvalidOperationException("Unity 改变了对象引用轨道，本次操作已撤销。");
                EditorUtility.SetDirty(clip);
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static ClipSnapshot CaptureClip(AnimationClip clip)
        {
            var snapshot = new ClipSnapshot();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                snapshot.Curves.Add(BindingKey(binding),
                    CloneCurve(AnimationUtility.GetEditorCurve(clip, binding)));
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                snapshot.ObjectCurves.Add(BindingKey(binding),
                    AnimationUtility.GetObjectReferenceCurve(clip, binding));
            return snapshot;
        }

        internal static bool CurvesEqual(AnimationCurve left, AnimationCurve right)
        {
            if (left == null || right == null) return left == right;
            if (left.length != right.length || left.preWrapMode != right.preWrapMode ||
                left.postWrapMode != right.postWrapMode) return false;
            Keyframe[] a = left.keys;
            Keyframe[] b = right.keys;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].time.Equals(b[i].time) || !a[i].value.Equals(b[i].value) ||
                    !a[i].inTangent.Equals(b[i].inTangent) || !a[i].outTangent.Equals(b[i].outTangent) ||
                    !a[i].inWeight.Equals(b[i].inWeight) || !a[i].outWeight.Equals(b[i].outWeight) ||
                    a[i].weightedMode != b[i].weightedMode ||
                    AnimationUtility.GetKeyBroken(left, i) != AnimationUtility.GetKeyBroken(right, i) ||
                    AnimationUtility.GetKeyLeftTangentMode(left, i) != AnimationUtility.GetKeyLeftTangentMode(right, i) ||
                    AnimationUtility.GetKeyRightTangentMode(left, i) != AnimationUtility.GetKeyRightTangentMode(right, i))
                    return false;
            }
            return true;
        }

        private static bool ObjectCurvesEqual(
            Dictionary<string, ObjectReferenceKeyframe[]> left,
            Dictionary<string, ObjectReferenceKeyframe[]> right)
        {
            if (left.Count != right.Count) return false;
            foreach (var entry in left)
            {
                ObjectReferenceKeyframe[] actual;
                if (!right.TryGetValue(entry.Key, out actual) || entry.Value.Length != actual.Length)
                    return false;
                for (int i = 0; i < actual.Length; i++)
                    if (!entry.Value[i].time.Equals(actual[i].time) || entry.Value[i].value != actual[i].value)
                        return false;
            }
            return true;
        }

        internal static AnimationCurve CloneCurve(AnimationCurve curve)
        {
            if (curve == null) return null;
            return new AnimationCurve(curve.keys)
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode
            };
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null) return null;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, Members);
            if (property != null) return property.GetValue(instance, null);
            FieldInfo field = type.GetField(name, Members);
            return field != null ? field.GetValue(instance) : null;
        }

        internal static List<string> BindingKeys(List<EditorCurveBinding> bindings)
        {
            var result = new List<string>();
            foreach (EditorCurveBinding binding in bindings) result.Add(BindingKey(binding));
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        internal static string BindingKey(EditorCurveBinding binding)
        {
            return binding.path + "|" + (binding.type == null ? "" : binding.type.FullName) +
                "|" + binding.propertyName + "|" + binding.isPPtrCurve + "|" + binding.isDiscreteCurve;
        }

        private static bool StringListsEqual(List<string> left, List<string> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i] != right[i]) return false;
            return true;
        }

        private static bool IsAxis(string property, string prefix)
        {
            return property == prefix + ".x" || property == prefix + ".y" || property == prefix + ".z";
        }

        private static string MakeLabel(EditorCurveBinding binding)
        {
            string property = binding.propertyName ?? "";
            string axis = property.Length > 0 ? property.Substring(property.LastIndexOf('.') + 1).ToUpperInvariant() : "";
            string kind = property.StartsWith("m_LocalScale.", StringComparison.Ordinal) ? "缩放 " :
                property.StartsWith("m_AnchoredPosition.", StringComparison.Ordinal) ? "UI 位置 " :
                property.StartsWith("m_LocalPosition.", StringComparison.Ordinal) ? "位置 " : property;
            string path = string.IsNullOrEmpty(binding.path) ? "(根对象)" : binding.path;
            return path + " · " + (kind == property ? property : kind + axis);
        }

        private static string AppendNotice(string current, string addition)
        {
            return string.IsNullOrEmpty(current) ? addition : current + "\n" + addition;
        }

        internal static string FriendlyError(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
                exception = exception.InnerException;
            return string.IsNullOrEmpty(exception.Message) ? "无法完成对齐操作，请重新刷新。" : exception.Message;
        }

        internal static void RethrowExitGUI(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                if (current is ExitGUIException)
                    ExceptionDispatchInfo.Capture(current).Throw();
                current = current.InnerException;
            }
        }
    }
}
