using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using URflow;

public static class URFlowServiceChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static AnimationWindow _window;
    static object _state;
    static AnimationClip _clip;
    static GameObject _root;
    static GameObject _fixtureRoot;
    static int _passed;
    static int _frames;
    static bool _injectedWriteChange;
    static readonly List<string> _results = new List<string>();
    static EditorCurveBinding X { get { return EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"); } }
    static EditorCurveBinding Y { get { return EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"); } }

    public static void Run()
    {
        _passed = 0;
        _frames = 0;
        _results.Clear();
        EditorSceneManager.OpenScene("Assets/ValidationAssets/Alignment.unity", OpenSceneMode.Single);
        _clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ValidationAssets/Alignment.anim");
        _fixtureRoot = GameObject.Find("URFlow Alignment Test");
        Assert(_clip != null && _fixtureRoot != null, "Bootstrap scene is missing.");
        Selection.activeGameObject = _fixtureRoot;
        _window = EditorWindow.GetWindow<AnimationWindow>();
        _window.Show();
        _window.animationClip = _clip;
        EditorApplication.update += OnReady;
    }

    static void OnReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || ++_frames < 12) return;
        _state = Get(_window, "state") ?? Get(Get(_window, "m_AnimEditor"), "state");
        if (_state == null || Get(_state, "hierarchyState") == null || Get(_state, "hierarchyData") == null)
        {
            _window.Repaint();
            if (_frames < 300) return;
            EditorApplication.update -= OnReady;
            var error = new InvalidOperationException("Actual AnimationWindow hierarchy did not initialize after 300 editor frames.");
            WriteReport(false, error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.update -= OnReady;
        try
        {
            Assert(Event.current == null, "Service checks must execute outside OnGUI.");
            Check("single selected key creates read-only preview of whole property", PreviewIsReadOnly);
            Check("translation changes entire selected curve and preserves other axes", Translation);
            Check("last anchor proportional conversion and complete curve write", Proportional);
            Check("single Undo and Redo restore the complete selected property", UndoRedo);
            Check("changed static target rejects stale preview", StaticChanged);
            Check("changed source curve rejects stale preview", SourceChanged);
            Check("changed property selection rejects stale preview", SelectionChanged);
            Check("changed animation root rejects stale preview", RootChanged);
            Check("no property selection is rejected", NoSelection);
            Check("mixed supported and unsupported properties are rejected atomically", MixedUnsupported);
            Check("already aligned application is a no-op", NoOp);
            Check("existing READ/APPLY and CurveGuard retain normalized curve behavior", ExistingReadApplyGuard);
            Check("hierarchy rows and key selections cannot silently expand target properties", HierarchyKeyAuthority);
            Check("sampled preview reads static values and restores time without recording", PreviewSampling);
            Check("RectTransform anchored axes and scale use actual static values", RectTransformValues);
            Check("native driven RectTransform axis is rejected without writing", DrivenRectTransformAxis);
            Check("constant tangent curve writes and survives guard ticks and Undo/Redo", ConstantCurveWrite);
            Check("failed stale apply closes recording and retains the notice without writing", FailedApplyNotice);
            Check("post-write verification failure rolls back every property atomically", AtomicRollback);
            WriteReport(true, null);
            Debug.Log("URFLOW_SERVICE_CHECKS_PASS " + _passed);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            WriteReport(false, e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void Check(string name, Action action)
    {
        try
        {
            ResetFixture();
            action();
            _passed++;
            _results.Add("PASS: " + name);
            Debug.Log("PASS: " + name);
        }
        catch (Exception e) { throw new InvalidOperationException("Service check failed: " + name, e); }
    }

    static void ResetFixture()
    {
        URflowCurveGuard.ClearAll();
        Undo.ClearAll();
        if (_window != null) _window.previewing = false;
        _root = _fixtureRoot;
        for (int i = _root.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(_root.transform.GetChild(i).gameObject);
        _root.transform.localPosition = new Vector3(10, -5, 7);
        _root.transform.localScale = new Vector3(3, 4, 5);
        _clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ValidationAssets/Alignment.anim");
        Assert(_clip != null, "Bootstrap clip is missing.");
        foreach (var binding in AnimationUtility.GetCurveBindings(_clip)) AnimationUtility.SetEditorCurve(_clip, binding, null);
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(_clip)) AnimationUtility.SetObjectReferenceCurve(_clip, binding, null);
        AnimationUtility.SetEditorCurve(_clip, X, MakeCurve(2, 6, 4));
        AnimationUtility.SetEditorCurve(_clip, Y, MakeCurve(-3, -1, -2));
        AnimationUtility.SetEditorCurve(_clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), MakeCurve(7, 9, 8));
        Selection.activeGameObject = _root;
        if (_window == null) _window = EditorWindow.GetWindow<AnimationWindow>();
        _window.previewing = false;
        _window.recording = false;
        _state = Get(_window, "state") ?? Get(Get(_window, "m_AnimEditor"), "state");
        Assert(_state != null, "Actual AnimationWindow state is unavailable.");
        SetRootSelection(_root);
        SelectBindings(X);
        _root.transform.localPosition = new Vector3(10, -5, 7);
    }

    static AnimationCurve MakeCurve(float first, float middle, float last)
    {
        var c = new AnimationCurve(new Keyframe(0, first, 2, 2), new Keyframe(1, middle, -1, -1), new Keyframe(2, last, 1, 1));
        c.preWrapMode = WrapMode.ClampForever;
        c.postWrapMode = WrapMode.ClampForever;
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyBroken(c, i, true);
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Free);
        }
        return c;
    }

    static void SetRootSelection(GameObject root)
    {
        Selection.activeGameObject = root;
        Invoke(_window, "OnSelectionChange");
        _window.animationClip = _clip;
        _state = Get(_window, "state") ?? Get(Get(_window, "m_AnimEditor"), "state");
        Invoke(_state, "ForceRefresh");
        Invoke(_state, "Refresh");
        Assert(Get(_state, "activeRootGameObject") as GameObject == root, "Actual AnimationWindow root did not match fixture.");
        Assert(Get(_state, "activeAnimationClip") as AnimationClip == _clip, "Actual AnimationWindow clip did not match fixture.");
    }

    static void SelectBindings(params EditorCurveBinding[] bindings)
    {
        Invoke(_state, "ClearSelections");
        Invoke(_state, "ForceRefresh");
        Invoke(_state, "Refresh");
        var curves = Get(_state, "allCurves") as IEnumerable;
        Assert(curves != null, "Actual AnimationWindow curves are unavailable.");
        foreach (var binding in bindings)
        {
            object selectedCurve = null;
            foreach (var curve in curves)
            {
                var candidate = Get(curve, "binding");
                if (candidate is EditorCurveBinding && SameBinding((EditorCurveBinding)candidate, binding)) { selectedCurve = curve; break; }
            }
            Assert(selectedCurve != null, "Actual AnimationWindow curve not found: " + binding.propertyName);
            var keys = Get(selectedCurve, "keyframes") as IList;
            Assert(keys != null && keys.Count > 0, "Actual AnimationWindow keys not available: " + binding.propertyName);
            Invoke(_state, "SelectKey", keys[0]);
        }
        var selected = Get(_state, "selectedKeys") as IList;
        Assert(selected != null && selected.Count == bindings.Length, "Actual key selection count did not match.");
    }

    static void SelectFirstPair()
    {
        SelectBindings(X);
        foreach (var curve in (IEnumerable)Get(_state, "allCurves"))
        {
            var binding = Get(curve, "binding");
            if (!(binding is EditorCurveBinding) || !SameBinding((EditorCurveBinding)binding, X)) continue;
            var keys = Get(curve, "keyframes") as IList;
            Assert(keys != null && keys.Count >= 2, "Two real keys are required.");
            Invoke(_state, "SelectKey", keys[1]);
            break;
        }
    }

    static URflowKeyframeMovePreview Preview(URflowKeyframeMoveAnchor anchor = URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode mode = URflowKeyframeMoveMode.Translate)
    {
        URflowKeyframeMovePreview preview;
        string error;
        Assert(URflowKeyframeMoveService.TryCreatePreview(anchor, mode, out preview, out error), "Preview rejected: " + error);
        Assert(preview != null && preview.IsValid && preview.Rows.Count == 1 && SameBinding(preview.Rows[0].Binding, X), "Preview selection was not exactly X.");
        return preview;
    }

    static void Apply(URflowKeyframeMovePreview preview, int expectedCount = 1)
    {
        int count;
        string error;
        Assert(URflowKeyframeMoveService.TryApply(preview, out count, out error), "Apply rejected: " + error);
        Assert(count == expectedCount, "Unexpected modified property count: " + count);
    }

    static void PreviewIsReadOnly()
    {
        var beforeX = AnimationUtility.GetEditorCurve(_clip, X);
        var beforeY = AnimationUtility.GetEditorCurve(_clip, Y);
        var preview = Preview();
        Assert(preview.Root == _root && preview.Clip == _clip, "Preview captured wrong context.");
        Near(preview.Rows[0].Result.OriginalAnchorValue, 2, "Original anchor");
        Near(preview.Rows[0].Result.TargetValue, 10, "Static target");
        Near(preview.Rows[0].Result.DeltaOrFactor, 8, "Delta");
        AssertCurve(beforeX, AnimationUtility.GetEditorCurve(_clip, X), "Preview changed X");
        AssertCurve(beforeY, AnimationUtility.GetEditorCurve(_clip, Y), "Preview changed Y");
    }

    static void Translation()
    {
        var y = AnimationUtility.GetEditorCurve(_clip, Y);
        var preview = Preview();
        Apply(preview);
        AssertCurve(preview.Rows[0].Result.Curve, AnimationUtility.GetEditorCurve(_clip, X), "Stored translation differs");
        AssertCurve(y, AnimationUtility.GetEditorCurve(_clip, Y), "Unselected Y changed");
        Values(AnimationUtility.GetEditorCurve(_clip, X), 10, 14, 12);
    }

    static void Proportional()
    {
        var y = AnimationUtility.GetEditorCurve(_clip, Y);
        var preview = Preview(URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
        Near(preview.Rows[0].Result.DeltaOrFactor, 2.5f, "Factor");
        Apply(preview);
        Values(AnimationUtility.GetEditorCurve(_clip, X), 5, 15, 10);
        AssertCurve(preview.Rows[0].Result.Curve, AnimationUtility.GetEditorCurve(_clip, X), "Stored ratio differs");
        AssertCurve(y, AnimationUtility.GetEditorCurve(_clip, Y), "Unselected Y changed");
    }

    static void UndoRedo()
    {
        var x = AnimationUtility.GetEditorCurve(_clip, X);
        var y = AnimationUtility.GetEditorCurve(_clip, Y);
        var preview = Preview();
        Apply(preview);
        var applied = AnimationUtility.GetEditorCurve(_clip, X);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        AssertCurve(x, AnimationUtility.GetEditorCurve(_clip, X), "Undo did not restore X");
        AssertCurve(y, AnimationUtility.GetEditorCurve(_clip, Y), "Undo changed Y");
        Undo.PerformRedo();
        AssertCurve(applied, AnimationUtility.GetEditorCurve(_clip, X), "Redo did not restore X");
        AssertCurve(y, AnimationUtility.GetEditorCurve(_clip, Y), "Redo changed Y");
    }

    static void StaticChanged()
    {
        var preview = Preview();
        var source = AnimationUtility.GetEditorCurve(_clip, X);
        _root.transform.localPosition = new Vector3(11, -5, 7);
        Reject(preview, "Static target change");
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "Rejected apply changed source");
    }

    static void SourceChanged()
    {
        var preview = Preview();
        var changed = AnimationUtility.GetEditorCurve(_clip, X);
        var keys = changed.keys;
        keys[1].value += 1;
        changed.keys = keys;
        AnimationUtility.SetEditorCurve(_clip, X, changed);
        changed = AnimationUtility.GetEditorCurve(_clip, X);
        Reject(preview, "Source curve change");
        AssertCurve(changed, AnimationUtility.GetEditorCurve(_clip, X), "Rejected apply overwrote changed source");
    }

    static void SelectionChanged()
    {
        var preview = Preview();
        var source = AnimationUtility.GetEditorCurve(_clip, X);
        SelectBindings(Y);
        Reject(preview, "Property selection change");
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "Rejected selection apply changed X");
    }

    static void RootChanged()
    {
        var preview = Preview();
        var source = AnimationUtility.GetEditorCurve(_clip, X);
        var other = new GameObject("Other Verification Root");
        other.AddComponent<Animator>().runtimeAnimatorController = _root.GetComponent<Animator>().runtimeAnimatorController;
        other.transform.localPosition = _root.transform.localPosition;
        SetRootSelection(other);
        SelectBindings(X);
        Reject(preview, "Animation root change");
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "Rejected root apply changed X");
    }

    static void NoSelection()
    {
        Invoke(_state, "ClearSelections");
        URflowKeyframeMovePreview preview;
        string error;
        Assert(!URflowKeyframeMoveService.TryCreatePreview(URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate, out preview, out error), "No selection unexpectedly accepted.");
        Assert(!string.IsNullOrEmpty(error), "No-selection error is missing.");
    }

    static void MixedUnsupported()
    {
        var rotation = EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAnglesRaw.x");
        AnimationUtility.SetEditorCurve(_clip, rotation, MakeCurve(0, 40, 20));
        SelectBindings(X, rotation);
        var source = AnimationUtility.GetEditorCurve(_clip, X);
        URflowKeyframeMovePreview preview;
        string error;
        Assert(!URflowKeyframeMoveService.TryCreatePreview(URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate, out preview, out error), "Mixed unsupported properties unexpectedly accepted.");
        Assert(preview != null && !preview.IsValid && preview.Rows.Count == 2, "Unsupported preview did not retain both diagnostic rows.");
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "Invalid preview wrote supported X");
        Reject(preview, "Mixed invalid preview");
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "Invalid apply wrote supported X");
    }

    static void NoOp()
    {
        _root.transform.localPosition = new Vector3(2, -5, 7);
        var source = AnimationUtility.GetEditorCurve(_clip, X);
        var preview = Preview();
        Assert(!preview.Rows[0].Result.HasChanges, "Aligned preview should not change the curve.");
        Apply(preview, 0);
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "No-op changed curve");
    }

    static void ExistingReadApplyGuard()
    {
        SelectFirstPair();
        URflowApplyHelper.ApplyToAnimationWindow(.25f, .1f, .75f, .9f, 0);
        Assert(URflowCurveGuard.TrackedCount == 1, "Old APPLY did not register one selected segment.");
        float x1, y1, x2, y2;
        Assert(URflowReadHelper.ReadFromAnimationWindow(out x1, out y1, out x2, out y2), "Old READ failed.");
        Near(x1, .25f, "READ x1"); Near(y1, .1f, "READ y1"); Near(x2, .75f, "READ x2"); Near(y2, .9f, "READ y2");
        var preview = Preview();
        var expected = preview.Rows[0].Result.Curve;
        Apply(preview);
        Assert(URflowCurveGuard.TrackedCount == 1, "New alignment lost CurveGuard registration.");
        AssertCurve(expected, AnimationUtility.GetEditorCurve(_clip, X), "Alignment changed weighted curve metadata");
        TickGuard();
        AssertCurve(expected, AnimationUtility.GetEditorCurve(_clip, X), "CurveGuard overwrote aligned weighted curve");
        var stripped = AnimationUtility.GetEditorCurve(_clip, X);
        var keys = stripped.keys;
        keys[0].weightedMode = WeightedMode.None;
        keys[1].weightedMode = WeightedMode.None;
        stripped.keys = keys;
        AnimationUtility.SetEditorCurve(_clip, X, stripped);
        TickGuard();
        var restored = AnimationUtility.GetEditorCurve(_clip, X);
        Assert((restored.keys[0].weightedMode & WeightedMode.Out) != 0, "Guard did not restore first outgoing weight.");
        Assert((restored.keys[1].weightedMode & WeightedMode.In) != 0, "Guard did not restore second incoming weight.");
        Near(restored.keys[0].outWeight, .25f, "Guard outgoing weight");
        Near(restored.keys[1].inWeight, .25f, "Guard incoming weight");
        Near(restored.keys[0].outTangent, 1.6f, "Guard outgoing tangent");
        Near(restored.keys[1].inTangent, 1.6f, "Guard incoming tangent");
        Values(restored, 10, 14, 12);
    }

    static void TickGuard()
    {
        var method = typeof(URflowCurveGuard).GetMethod("OnEditorUpdate", BindingFlags.Static | BindingFlags.NonPublic);
        Assert(method != null, "CurveGuard tick method is unavailable.");
        for (int i = 0; i < 6; i++) method.Invoke(null, null);
    }

    static void PreviewSampling()
    {
        var source = AnimationUtility.GetEditorCurve(_clip, X);
        _root.transform.localPosition = new Vector3(10, -5, 7);
        _window.previewing = true;
        _window.time = 1.25f;
        Assert(_window.previewing, "Actual AnimationWindow could not start preview.");
        Assert(Mathf.Abs(_root.transform.localPosition.x - 10) > .01f, "Fixture root was not actually sampled away from its static value.");
        float sampled = _root.transform.localPosition.x;
        _window.recording = true;
        Assert(_window.recording, "Actual AnimationWindow could not start recording for the safety check.");
        var preview = Preview();
        Near(preview.Rows[0].Result.TargetValue, 10, "Static target while preview is sampled");
        Assert(_window.previewing && !_window.recording, "Preview was not restored or recording remained enabled.");
        Near(_window.time, 1.25f, "Preview time after refresh");
        Near(_root.transform.localPosition.x, sampled, "Sampled value after restoring preview");
        Assert(!string.IsNullOrEmpty(preview.Notice), "Recording shutdown notice was not reported.");
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "Refreshing sampled preview wrote the source clip");
        Apply(preview);
        Assert(_window.previewing && !_window.recording, "Applying did not restore preview safely.");
        Near(_window.time, 1.25f, "Preview time after apply");
        _window.previewing = false;
        Near(_root.transform.localPosition.x, 10, "Static value after leaving preview");
        Values(AnimationUtility.GetEditorCurve(_clip, X), 10, 14, 12);
    }

    static RectTransform AddRectFixture(out EditorCurveBinding anchoredX, out EditorCurveBinding anchoredY, out EditorCurveBinding scaleX)
    {
        var child = new GameObject("UI Element", typeof(RectTransform));
        var rect = child.GetComponent<RectTransform>();
        rect.SetParent(_root.transform, false);
        rect.anchoredPosition = new Vector2(30, -20);
        rect.localScale = new Vector3(2, 3, 4);
        anchoredX = EditorCurveBinding.FloatCurve("UI Element", typeof(RectTransform), "m_AnchoredPosition.x");
        anchoredY = EditorCurveBinding.FloatCurve("UI Element", typeof(RectTransform), "m_AnchoredPosition.y");
        scaleX = EditorCurveBinding.FloatCurve("UI Element", typeof(RectTransform), "m_LocalScale.x");
        AnimationUtility.SetEditorCurve(_clip, anchoredX, MakeCurve(2, 6, 4));
        AnimationUtility.SetEditorCurve(_clip, anchoredY, MakeCurve(-3, -1, -2));
        AnimationUtility.SetEditorCurve(_clip, scaleX, MakeCurve(.5f, 1, .75f));
        return rect;
    }

    static void RectTransformValues()
    {
        EditorCurveBinding ax, ay, sx;
        var rect = AddRectFixture(out ax, out ay, out sx);
        var rootX = AnimationUtility.GetEditorCurve(_clip, X);
        SelectBindings(ax, ay);
        URflowKeyframeMovePreview preview;
        string error;
        Assert(URflowKeyframeMoveService.TryCreatePreview(URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate, out preview, out error), "RectTransform preview rejected: " + error);
        Assert(preview.Rows.Count == 2, "RectTransform preview property count was wrong.");
        foreach (var row in preview.Rows) Near(row.Result.TargetValue, SameBinding(row.Binding, ax) ? 30 : -20, "RectTransform anchored static target");
        Apply(preview, 2);
        Values(AnimationUtility.GetEditorCurve(_clip, ax), 30, 34, 32);
        Values(AnimationUtility.GetEditorCurve(_clip, ay), -20, -18, -19);
        AssertCurve(rootX, AnimationUtility.GetEditorCurve(_clip, X), "RectTransform alignment modified root X");
        SelectBindings(sx);
        Assert(URflowKeyframeMoveService.TryCreatePreview(URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Proportional, out preview, out error), "Scale preview rejected: " + error);
        Near(preview.Rows[0].Result.TargetValue, 2, "RectTransform scale static target");
        Near(preview.Rows[0].Result.DeltaOrFactor, 4, "RectTransform scale ratio");
        Apply(preview);
        Values(AnimationUtility.GetEditorCurve(_clip, sx), 2, 4, 3);
        Near(rect.anchoredPosition.x, 30, "RectTransform static X changed");
    }

    static void DrivenRectTransformAxis()
    {
        EditorCurveBinding ax, ay, sx;
        var rect = AddRectFixture(out ax, out ay, out sx);
        var tracker = new DrivenRectTransformTracker();
        tracker.Add(_root.GetComponent<Animator>(), rect, DrivenTransformProperties.AnchoredPositionX);
        try
        {
            Assert(rect.drivenByObject != null, "Native driven RectTransform fixture was not registered.");
            Assert(AnimationUtility.GetAnimatedObject(_root, ax) == rect, "Driven fixture binding did not resolve to the tracked RectTransform.");
            SelectBindings(ax);
            var source = AnimationUtility.GetEditorCurve(_clip, ax);
            URflowKeyframeMovePreview preview;
            string error;
            Assert(!URflowKeyframeMoveService.TryCreatePreview(URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate, out preview, out error), "Driven axis unexpectedly accepted.");
            Assert(preview != null && preview.Rows.Count == 1 && !preview.IsValid && preview.Rows[0].Error.Contains("布局"), "Driven axis error was not specific.");
            AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, ax), "Driven axis rejection wrote the clip");
            Reject(preview, "Driven RectTransform axis");
            SelectBindings(ay);
            Assert(URflowKeyframeMoveService.TryCreatePreview(URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate, out preview, out error), "Undriven Y axis rejected by X-only driver: " + error);
        }
        finally { tracker.Clear(); }
    }

    static void ConstantCurveWrite()
    {
        var curve = MakeCurve(3, 3, 3);
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
        }
        AnimationUtility.SetEditorCurve(_clip, X, curve);
        SelectBindings(X);
        var source = AnimationUtility.GetEditorCurve(_clip, X);
        var preview = Preview();
        Apply(preview);
        var applied = AnimationUtility.GetEditorCurve(_clip, X);
        Values(applied, 10, 10, 10);
        AssertCurve(preview.Rows[0].Result.Curve, applied, "Stored constant curve differs");
        TickGuard();
        AssertCurve(applied, AnimationUtility.GetEditorCurve(_clip, X), "Guard tick changed a constant curve");
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "Constant curve Undo differs");
        Undo.PerformRedo();
        AssertCurve(applied, AnimationUtility.GetEditorCurve(_clip, X), "Constant curve Redo differs");
    }

    static void FailedApplyNotice()
    {
        var preview = Preview();
        var source = AnimationUtility.GetEditorCurve(_clip, X);
        _root.transform.localPosition = new Vector3(11, -5, 7);
        _window.previewing = true;
        _window.time = .5f;
        _window.recording = true;
        Assert(_window.previewing && _window.recording, "Failed apply recording fixture did not start.");
        Reject(preview, "Stale recording apply");
        Assert(!_window.recording && _window.previewing, "Failed apply did not close recording and restore preview.");
        Near(_window.time, .5f, "Failed apply preview time");
        Assert(!string.IsNullOrEmpty(preview.Notice) && preview.Notice.Contains("录制"), "Failed apply lost the recording shutdown notice.");
        AssertCurve(source, AnimationUtility.GetEditorCurve(_clip, X), "Failed recording apply wrote the clip");
        _window.previewing = false;
        Near(_root.transform.localPosition.x, 11, "Failed apply changed static value");
    }

    static void AtomicRollback()
    {
        var x = AnimationUtility.GetEditorCurve(_clip, X);
        var y = AnimationUtility.GetEditorCurve(_clip, Y);
        var preview = Preview();
        _injectedWriteChange = false;
        AnimationUtility.onCurveWasModified += InjectUnselectedChange;
        try
        {
            int count;
            string error;
            bool applied = URflowKeyframeMoveService.TryApply(preview, out count, out error);
            Assert(_injectedWriteChange, "Native curve-written callback was not invoked; rollback branch was not exercised.");
            Assert(!applied && count == 0 && error != null && error.Contains("撤销"), "Post-write mutation did not report an atomic rollback: " + error);
            AssertCurve(x, AnimationUtility.GetEditorCurve(_clip, X), "Atomic rollback did not restore selected X");
            AssertCurve(y, AnimationUtility.GetEditorCurve(_clip, Y), "Atomic rollback did not restore unselected Y");
            Debug.Log("Expected post-write rollback: " + error);
        }
        finally { AnimationUtility.onCurveWasModified -= InjectUnselectedChange; }
    }

    static void InjectUnselectedChange(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type)
    {
        if (_injectedWriteChange || clip != _clip) return;
        _injectedWriteChange = true;
        var y = AnimationUtility.GetEditorCurve(clip, Y);
        var keys = y.keys;
        keys[0].value += 99;
        y.keys = keys;
        AnimationUtility.SetEditorCurve(clip, Y, y);
    }

    static void HierarchyKeyAuthority()
    {
        var x = AnimationUtility.GetEditorCurve(_clip, X);
        var y = AnimationUtility.GetEditorCurve(_clip, Y);
        object data = Get(_state, "hierarchyData");
        object tree = Get(data, "root") ?? Get(data, "m_RootItem");
        Assert(tree != null, "Actual Animation hierarchy tree was not available.");
        var nodes = new List<object>();
        CollectNodes(tree, nodes);
        File.WriteAllText(Path.GetFullPath("Logs/hierarchy-nodes.txt"), string.Join("\n", nodes.ConvertAll(NodeSummary).ToArray()));
        object xNode = nodes.Find(n => NodeIsBinding(n, X));
        object yNode = nodes.Find(n => NodeIsBinding(n, Y));
        object group = nodes.Find(n => n.GetType().Name == "AnimationWindowHierarchyPropertyGroupNode" && GroupContains(n, X) && GroupContains(n, Y));
        Assert(xNode != null && yNode != null && group != null, "Actual position hierarchy nodes are missing.");

        SelectHierarchyNode(xNode, true);
        Assert(((IList)Get(_state, "selectedKeys")).Count == 0, "Selecting only a hierarchy row unexpectedly selected keys.");
        Preview();
        TraceSelection("only row X");
        AddFirstKey(X);
        Preview();
        TraceSelection("row X plus key X");

        SelectHierarchyNode(yNode, true);
        AddFirstKey(X);
        ProbeConflict("row Y plus key X", X);
        SelectHierarchyNode(group, true);
        AddFirstKey(X);
        ProbeConflict("Position group plus key X", X);

        SelectBindings(X);
        SelectHierarchyNode(yNode, false);
        TraceSelection("key X then selecting row Y");
        URflowKeyframeMovePreview preview;
        string error;
        bool accepted = URflowKeyframeMoveService.TryCreatePreview(URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate, out preview, out error);
        var remaining = Get(_state, "selectedKeys") as IList;
        if (remaining != null && remaining.Count > 0)
            Assert(!accepted, "Selecting Y with an old X key silently accepted an ambiguous target.");
        else
            Assert(accepted && preview.Rows.Count == 1 && SameBinding(preview.Rows[0].Binding, Y), "Row Y selection did not capture exactly Y.");
        AssertCurve(x, AnimationUtility.GetEditorCurve(_clip, X), "Selection authority probes changed X");
        AssertCurve(y, AnimationUtility.GetEditorCurve(_clip, Y), "Selection authority probes changed Y");
    }

    static void ProbeConflict(string label, EditorCurveBinding keyBinding)
    {
        TraceSelection(label);
        var nodes = Get(_state, "selectedHierarchyNodes") as IList;
        URflowKeyframeMovePreview preview;
        string error;
        bool accepted = URflowKeyframeMoveService.TryCreatePreview(URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate, out preview, out error);
        if (nodes != null && nodes.Count > 0)
            Assert(!accepted && !string.IsNullOrEmpty(error), label + " silently expanded or changed the target properties.");
        else
            Assert(accepted && preview.Rows.Count == 1 && SameBinding(preview.Rows[0].Binding, keyBinding), label + " did not capture the native cleared selection.");
        Debug.Log("Selection authority " + label + ": accepted=" + accepted + ", error=" + error);
    }

    static void SelectHierarchyNode(object node, bool clear)
    {
        if (clear) Invoke(_state, "ClearSelections");
        int id = (int)Get(node, "id");
        Set(Get(_state, "hierarchyState"), "selectedIDs", new List<int> { id });
        Invoke(_state, "OnHierarchySelectionChanged", new int[] { id });
    }

    static void AddFirstKey(EditorCurveBinding binding)
    {
        foreach (object curve in (IEnumerable)Get(_state, "allCurves"))
        {
            object candidate = Get(curve, "binding");
            if (!(candidate is EditorCurveBinding) || !SameBinding((EditorCurveBinding)candidate, binding)) continue;
            var keys = Get(curve, "keyframes") as IList;
            Assert(keys != null && keys.Count > 0, "Actual key was not available for selection authority probe.");
            Invoke(_state, "SelectKey", keys[0]);
            return;
        }
        throw new InvalidOperationException("Actual key curve missing: " + binding.propertyName);
    }

    static void TraceSelection(string label)
    {
        var parts = new List<string> { label };
        var nodes = Get(_state, "selectedHierarchyNodes") as IEnumerable;
        if (nodes != null) foreach (object node in nodes) parts.Add("NODE " + NodeSummary(node));
        var keys = Get(_state, "selectedKeys") as IEnumerable;
        if (keys != null) foreach (object key in keys)
        {
            object curve = Get(key, "curve");
            object binding = Get(curve, "binding");
            parts.Add("KEY " + (binding is EditorCurveBinding ? ((EditorCurveBinding)binding).propertyName : "unknown") + " time=" + Get(key, "time"));
        }
        string trace = string.Join("\n", parts.ToArray());
        File.AppendAllText(Path.GetFullPath("Logs/selection-authority.txt"), trace + "\n\n");
        Debug.Log(trace);
    }

    static void CollectNodes(object node, List<object> nodes)
    {
        if (node == null) return;
        nodes.Add(node);
        var children = Get(node, "children") as IEnumerable;
        if (children != null) foreach (object child in children) CollectNodes(child, nodes);
    }
    static bool NodeIsBinding(object node, EditorCurveBinding binding)
    {
        if (node.GetType().Name != "AnimationWindowHierarchyPropertyNode") return false;
        object candidate = Get(node, "binding");
        return candidate is EditorCurveBinding && SameBinding((EditorCurveBinding)candidate, binding);
    }
    static bool GroupContains(object node, EditorCurveBinding binding)
    {
        var curves = Get(node, "curves") as IEnumerable;
        if (curves == null) return false;
        foreach (object curve in curves)
        {
            object candidate = Get(curve, "binding");
            if (candidate is EditorCurveBinding && SameBinding((EditorCurveBinding)candidate, binding)) return true;
        }
        return false;
    }
    static string NodeSummary(object node)
    {
        object binding = Get(node, "binding");
        return node.GetType().Name + " id=" + Get(node, "id") + " property=" + Get(node, "propertyName") + " binding=" + (binding is EditorCurveBinding ? ((EditorCurveBinding)binding).propertyName : "none");
    }

    static void Reject(URflowKeyframeMovePreview preview, string label)
    {
        int count;
        string error;
        Assert(!URflowKeyframeMoveService.TryApply(preview, out count, out error), label + " was not rejected.");
        Assert(count == 0 && !string.IsNullOrEmpty(error), label + " rejection result is incomplete.");
        Debug.Log("Expected rejection (" + label + "): " + error);
    }

    static void Values(AnimationCurve curve, params float[] expected)
    {
        Assert(curve != null && curve.length == expected.Length, "Unexpected key count.");
        for (int i = 0; i < expected.Length; i++) Near(curve.keys[i].value, expected[i], "Key " + i + " value");
    }

    static void AssertCurve(AnimationCurve expected, AnimationCurve actual, string message)
    {
        Assert(expected != null && actual != null && expected.length == actual.length, message + " (structure)");
        Assert(expected.preWrapMode == actual.preWrapMode && expected.postWrapMode == actual.postWrapMode, message + " (wrap)");
        for (int i = 0; i < expected.length; i++)
        {
            Keyframe a = expected.keys[i], b = actual.keys[i];
            Near(b.time, a.time, message + " time " + i);
            Near(b.value, a.value, message + " value " + i);
            Near(b.inTangent, a.inTangent, message + " in tangent " + i);
            Near(b.outTangent, a.outTangent, message + " out tangent " + i);
            Near(b.inWeight, a.inWeight, message + " in weight " + i);
            Near(b.outWeight, a.outWeight, message + " out weight " + i);
            Assert(a.weightedMode == b.weightedMode, message + " weight mode " + i);
            Assert(AnimationUtility.GetKeyBroken(expected, i) == AnimationUtility.GetKeyBroken(actual, i), message + " Broken " + i);
            Assert(AnimationUtility.GetKeyLeftTangentMode(expected, i) == AnimationUtility.GetKeyLeftTangentMode(actual, i), message + " left mode " + i);
            Assert(AnimationUtility.GetKeyRightTangentMode(expected, i) == AnimationUtility.GetKeyRightTangentMode(actual, i), message + " right mode " + i);
        }
    }

    static bool SameBinding(EditorCurveBinding a, EditorCurveBinding b) { return a.path == b.path && a.type == b.type && a.propertyName == b.propertyName && a.isPPtrCurve == b.isPPtrCurve; }
    static Type FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { var type = assembly.GetType(name); if (type != null) return type; }
        throw new Exception("Internal Unity type missing: " + name);
    }
    static object Get(object target, string name)
    {
        if (target == null) return null;
        var property = target.GetType().GetProperty(name, Flags);
        if (property != null) return property.GetValue(target, null);
        var field = target.GetType().GetField(name, Flags);
        return field != null ? field.GetValue(target) : null;
    }
    static void Set(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(name, Flags);
        if (property != null && property.CanWrite) { property.SetValue(target, value, null); return; }
        var field = target.GetType().GetField(name, Flags);
        if (field != null) { field.SetValue(target, value); return; }
        throw new Exception("Internal Unity setter missing: " + target.GetType().Name + "." + name);
    }
    static object Invoke(object target, string name, params object[] values)
    {
        foreach (var method in target.GetType().GetMethods(Flags))
        {
            if (method.Name != name || method.GetParameters().Length != values.Length) continue;
            bool match = true;
            var parameters = method.GetParameters();
            for (int i = 0; i < values.Length; i++) if (values[i] != null && !parameters[i].ParameterType.IsInstanceOfType(values[i])) match = false;
            if (match) return method.Invoke(target, values);
        }
        throw new Exception("Internal Unity method missing: " + target.GetType().Name + "." + name);
    }
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Near(float actual, float expected, string message)
    {
        if (actual.Equals(expected)) return;
        if (float.IsNaN(actual) || float.IsInfinity(actual) || float.IsInfinity(expected) || Mathf.Abs(actual - expected) > 0.00005f * Mathf.Max(1, Mathf.Abs(expected)))
            throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
    }
    static void WriteReport(bool passed, Exception error)
    {
        string report = "Unity=" + Application.unityVersion + "\nServiceChecks=" + _passed + "\nResult=" + (passed ? "PASS" : "FAIL") + "\n" + string.Join("\n", _results.ToArray());
        if (error != null) report += "\n" + error;
        File.WriteAllText(Path.GetFullPath("Logs/service-result.txt"), report);
    }
}
