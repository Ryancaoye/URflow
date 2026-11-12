using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    public static class URFlowCurveApplyRegressionChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string Evidence = "D:/插件/URFLOW/6-开发/暂存资料/URFlow-v1.0.4-ui-20261109/evidence/curve-apply-20261112-result.txt";
        private static readonly StringBuilder Report = new StringBuilder();
        private static readonly List<AnimationClip> Clips = new List<AnimationClip>();
        private static AnimationWindow window;
        private static GameObject root;
        private static AnimationClip nativeClip;
        private static UnityEngine.Object[] oldSelection;
        private static double started;
        private static int ticks, checks;
        private static EditorCurveBinding X { get { return EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"); } }

        public static void Run()
        {
            oldSelection = Selection.objects;
            Report.AppendLine("Unity=" + Application.unityVersion);
            Report.AppendLine("Scope=Real AnimationCurve/AnimationClip APIs, Undo/Redo, CurveGuard update, and actual Animation-window selected keys + READ/APPLY. Transient independent test objects only; no production assets or pixel visual QA.");
            try
            {
                for (int flags = 0; flags < 4; flags++)
                    for (int mode = 0; mode < 3; mode++) BoundaryCase((WeightedMode)flags, mode);
                UnbrokenBoundary();
                WrapAndSelection();
                MultipleSelectedKeys();
                AdjacentGuard();
                SingleSideGuard();
                LostSideGuard();
                StructuralGuard();
                UndoRedo();

                nativeClip = NewClip("URFlow native selected pair");
                var keys = new[] { new Keyframe(0f, 0f, 1f, 2f), new Keyframe(1f, 6f, 1f, -3f), new Keyframe(2f, 8f, 2f, 0f) };
                for (int i = 0; i < keys.Length; i++) { keys[i].weightedMode = WeightedMode.Both; keys[i].inWeight = .14f + .06f * i; keys[i].outWeight = .71f - .08f * i; }
                SetCurve(nativeClip, new AnimationCurve(keys));
                root = new GameObject("URFlow temporary APPLY regression");
                var animation = root.AddComponent<Animation>();
                animation.playAutomatically = false;
                animation.AddClip(nativeClip, nativeClip.name);
                animation.clip = nativeClip;
                Selection.activeGameObject = root;
                window = EditorWindow.GetWindow<AnimationWindow>(); window.Show(); window.animationClip = nativeClip;
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += NativeReady;
            }
            catch (Exception error) { Finish(error); }
        }

        private static void BoundaryCase(WeightedMode flags, int mode)
        {
            URflowCurveGuard.ClearAll();
            AnimationClip clip = NewClip("URFlow boundary " + flags + " mode " + mode);
            SetCurve(clip, Fixture(flags));
            AnimationCurve before = Curve(clip);
            Require(URflowApplyHelper.ApplyToClip(clip, X, new[] { 1, 2 }, .2f, .65f, .78f, .22f, mode), "APPLY accepts " + flags + " mode " + mode);
            AnimationCurve after = Curve(clip);
            Outside(before, after, 1, 2, "boundary " + flags + " mode " + mode);
            if (mode == 2) Side(before, after, 1, false, "Incoming-only leaves A.out untouched");
            else { Near(after.keys[1].outWeight, .2f, "Selected A.out weight"); Require((after.keys[1].weightedMode & WeightedMode.Out) != 0, "Selected A.out weighted flag"); }
            if (mode == 1) Side(before, after, 2, true, "Outgoing-only leaves B.in untouched");
            else { Near(after.keys[2].inWeight, .22f, "Selected B.in weight"); Require((after.keys[2].weightedMode & WeightedMode.In) != 0, "Selected B.in weighted flag"); }
            TickGuard(); Full(after, Curve(clip), "Guard does not rewrite weighted APPLY " + flags + " mode " + mode);
        }

        private static void UnbrokenBoundary()
        {
            URflowCurveGuard.ClearAll();
            AnimationClip clip = NewClip("URFlow unbroken Auto boundary");
            AnimationCurve source = Fixture(WeightedMode.None);
            for (int i = 0; i < source.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(source, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(source, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyBroken(source, i, false);
            }
            SetCurve(clip, source); AnimationCurve before = Curve(clip);
            Require(URflowApplyHelper.ApplyToClip(clip, X, new[] { 1, 2 }, .25f, .1f, .75f, .9f, 0), "APPLY accepts unbroken Auto keys");
            Outside(before, Curve(clip), 1, 2, "Unbroken Auto outside segments retain their geometry and modes");
        }

        private static void WrapAndSelection()
        {
            var source = Fixture(WeightedMode.Both); source.preWrapMode = WrapMode.Loop; source.postWrapMode = WrapMode.PingPong;
            var selected = new List<int> { 2, 1, 2, -1, 99 }; AnimationCurve applied; List<URflowApplyHelper.AppliedPair> pairs;
            Require(URflowApplyHelper.TryBuildAppliedCurve(source, selected, .3f, .2f, .7f, .8f, 0, out applied, out pairs), "Builder filters duplicate/out-of-range selection");
            Require(selected.Count == 5 && selected[0] == 2 && selected[1] == 1 && pairs.Count == 1, "Builder never mutates Animation selection list");
            Require(applied.preWrapMode == WrapMode.Loop && applied.postWrapMode == WrapMode.PingPong, "Builder preserves pre/post wrap modes");
            Require(!URflowApplyHelper.TryBuildAppliedCurve(source, new[] { 1 }, .3f, .2f, .7f, .8f, 0, out applied, out pairs), "Builder rejects a single selected key");
        }

        private static void MultipleSelectedKeys()
        {
            URflowCurveGuard.ClearAll(); var clip = NewClip("URFlow multiple selected keys"); SetCurve(clip, Fixture(WeightedMode.Both));
            var before = Curve(clip);
            Require(URflowApplyHelper.ApplyToClip(clip, X, new[] { 0, 1, 2 }, .25f, .1f, .7f, .9f, 0), "Three selected keys apply two segments");
            var after = Curve(clip); Side(before, after, 2, false, "Last selected key retains outgoing side"); FullKey(before, after, 3, "Unselected terminal key"); Samples(before, after, 2f, 3f, "Unselected terminal segment");
            Require(URflowCurveGuard.TrackedCount == 2, "Both selected segments are guarded"); TickGuard(); Full(after, Curve(clip), "Multiple selected pair guard remains stable");
        }

        private static void AdjacentGuard()
        {
            URflowCurveGuard.ClearAll(); var clip = NewClip("URFlow adjacent guards"); SetCurve(clip, Fixture(WeightedMode.Both));
            URflowApplyHelper.ApplyToClip(clip, X, new[] { 2, 3 }, .15f, .3f, .8f, .9f, 0);
            var before = Curve(clip);
            URflowApplyHelper.ApplyToClip(clip, X, new[] { 1, 2 }, .25f, .6f, .7f, .2f, 0);
            var after = Curve(clip); Outside(before, after, 1, 2, "APPLY next to an existing Guard");
            Require(URflowCurveGuard.TrackedCount == 2, "Adjacent guards retain opposite side ownership");
            TickGuard(); Full(after, Curve(clip), "Old adjacent Guard cannot rewrite untouched outgoing side");
            var stripped = Curve(clip); var keys = stripped.keys; keys[1].weightedMode &= ~WeightedMode.Out; keys[2].weightedMode &= ~WeightedMode.In; stripped.keys = keys; SetCurve(clip, stripped);
            TickGuard(); var restored = Curve(clip); Full(after, restored, "Lost selected sides restore without changing the adjacent protected segment");
            Outside(before, restored, 1, 2, "Adjacent segment remains identical after restoration");
        }

        private static void SingleSideGuard()
        {
            for (int mode = 1; mode <= 2; mode++)
            {
                URflowCurveGuard.ClearAll(); var clip = NewClip("URFlow single side guard " + mode); SetCurve(clip, Fixture(WeightedMode.None));
                URflowApplyHelper.ApplyToClip(clip, X, new[] { 1, 2 }, .25f, .1f, .75f, .9f, mode);
                var after = Curve(clip); TickGuard(); Full(after, Curve(clip), "Single-side Guard ignores unowned unweighted side " + mode);
                var stripped = Curve(clip); var keys = stripped.keys;
                if (mode == 1) keys[1].weightedMode &= ~WeightedMode.Out; else keys[2].weightedMode &= ~WeightedMode.In;
                stripped.keys = keys; SetCurve(clip, stripped); TickGuard(); Full(after, Curve(clip), "Single-side Guard restores only applied direction " + mode);
            }
        }

        private static void LostSideGuard()
        {
            URflowCurveGuard.ClearAll(); var clip = NewClip("URFlow one lost side"); SetCurve(clip, Fixture(WeightedMode.Both));
            URflowApplyHelper.ApplyToClip(clip, X, new[] { 1, 2 }, .25f, .1f, .75f, .9f, 0);
            var edited = Curve(clip); var keys = edited.keys; keys[1].outTangent = 13f; keys[1].outWeight = .43f; keys[2].weightedMode &= ~WeightedMode.In; edited.keys = keys; SetCurve(clip, edited);
            var before = Curve(clip); TickGuard(); var after = Curve(clip);
            Side(before, after, 1, false, "Guard preserves deliberate weighted tangent edits while restoring the other side");
            Near(after.keys[2].inWeight, .25f, "Lost B.in is restored"); Outside(before, after, 1, 2, "One-side restoration outside segments");
        }

        private static void StructuralGuard()
        {
            URflowCurveGuard.ClearAll(); var clip = NewClip("URFlow guard key count"); SetCurve(clip, Fixture(WeightedMode.Both));
            URflowApplyHelper.ApplyToClip(clip, X, new[] { 1, 2 }, .25f, .1f, .75f, .9f, 0);
            var changed = Curve(clip); changed.AddKey(new Keyframe(.5f, 2f)); SetCurve(clip, changed); var before = Curve(clip);
            TickGuard(); Full(before, Curve(clip), "Inserted key cannot receive a stale indexed guard"); Require(URflowCurveGuard.TrackedCount == 0, "Structural change discards stale guard");
        }

        private static void UndoRedo()
        {
            URflowCurveGuard.ClearAll(); var clip = NewClip("URFlow APPLY Undo Redo"); SetCurve(clip, Fixture(WeightedMode.None));
            var before = Curve(clip); Undo.IncrementCurrentGroup();
            URflowApplyHelper.ApplyToClip(clip, X, new[] { 1, 2 }, .25f, .1f, .75f, .9f, 0); Undo.FlushUndoRecordObjects(); var applied = Curve(clip);
            Undo.PerformUndo(); Full(before, Curve(clip), "Undo restores full original curve"); TickGuard(); Full(before, Curve(clip), "Guard never resurrects an undone APPLY"); Require(URflowCurveGuard.TrackedCount == 0, "Undo clears stale guard snapshots");
            Undo.PerformRedo(); Full(applied, Curve(clip), "Redo restores applied curve"); TickGuard(); Full(applied, Curve(clip), "Redo stays stable through Guard update");
        }

        private static void NativeReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 40d) throw new InvalidOperationException("Native Animation context timed out.");
                if (++ticks < 3) return;
                Invoke(window, "OnSelectionChange"); window.animationClip = nativeClip;
                object state = Get(window, "state") ?? Get(Get(window, "m_AnimEditor"), "state");
                if (state == null) return;
                Invoke(state, "ForceRefresh"); Invoke(state, "Refresh");
                if (Get(state, "activeRootGameObject") as GameObject != root || Get(state, "activeAnimationClip") as AnimationClip != nativeClip) return;
                Invoke(state, "ClearSelections");
                object selectedCurve = null;
                foreach (object candidate in (IEnumerable)Get(state, "allCurves"))
                {
                    object binding = Get(candidate, "binding");
                    if (binding is EditorCurveBinding && ((EditorCurveBinding)binding).propertyName == X.propertyName) { selectedCurve = candidate; break; }
                }
                Require(selectedCurve != null, "Actual Animation window exposes the X fixture");
                var keys = (IList)Get(selectedCurve, "keyframes"); Invoke(state, "SelectKey", keys[0]); Invoke(state, "SelectKey", keys[1]);
                var selected = URflowAnimHelper.GetSelectedKeyframes(state);
                Require(selected.Count == 1 && selected[0].selectedIndices.Count == 2 && selected[0].selectedIndices.Contains(0) && selected[0].selectedIndices.Contains(1), "Actual native selection resolves exactly the first two of three keys");
                URflowCurveGuard.ClearAll(); var before = Curve(nativeClip);
                URflowApplyHelper.ApplyToAnimationWindow(.22f, .14f, .72f, .86f, 0); var after = Curve(nativeClip);
                Side(before, after, 1, false, "Native APPLY retains the second key's outgoing side"); FullKey(before, after, 2, "Native APPLY preserves unselected third key"); Samples(before, after, 1f, 2f, "Native APPLY preserves second-to-third segment");
                Require(after.keys[1].weightedMode == WeightedMode.Both, "Native APPLY retains Both weighted flags at the shared key");
                float x1, y1, x2, y2; Require(URflowReadHelper.ReadFromAnimationWindow(out x1, out y1, out x2, out y2), "READ succeeds on actual native selected pair");
                Near(x1, .22f, "READ X1"); Near(y1, .14f, "READ Y1"); Near(x2, .72f, "READ X2"); Near(y2, .86f, "READ Y2");
                TickGuard(); Full(after, Curve(nativeClip), "Native APPLY stays unchanged after Guard ticks");
                NativeReads(state); Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static void NativeReads(object state)
        {
            for (int flags = 0; flags < 4; flags++)
            {
                URflowCurveGuard.ClearAll();
                bool weightedOut = (flags & 1) != 0, weightedIn = (flags & 2) != 0;
                float expectedX1 = weightedOut ? .22f : 1f / 3f;
                float expectedX2 = 1f - (weightedIn ? .28f : 1f / 3f);
                const float expectedY1 = -4.75f, expectedY2 = 6.25f;
                var source = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 6f), new Keyframe(2f, 8f));
                for (int i = 0; i < source.length; i++) { AnimationUtility.SetKeyBroken(source, i, true); AnimationUtility.SetKeyLeftTangentMode(source, i, AnimationUtility.TangentMode.Free); AnimationUtility.SetKeyRightTangentMode(source, i, AnimationUtility.TangentMode.Free); }
                var keys = source.keys;
                keys[0].weightedMode = weightedOut ? WeightedMode.Both : WeightedMode.In;
                keys[1].weightedMode = weightedIn ? WeightedMode.Both : WeightedMode.Out;
                keys[0].outWeight = .22f; keys[1].inWeight = .28f;
                keys[0].outTangent = expectedY1 / expectedX1 * 6f;
                keys[1].inTangent = (1f - expectedY2) / (1f - expectedX2) * 6f;
                source.keys = keys; SetCurve(nativeClip, source);
                Invoke(state, "ForceRefresh"); Invoke(state, "Refresh"); Invoke(state, "ClearSelections");
                SelectNativePair(state);
                AnimationCurve before = Curve(nativeClip);
                float x1, y1, x2, y2;
                Require(URflowReadHelper.ReadFromAnimationWindow(out x1, out y1, out x2, out y2), "Actual READ accepts finite extreme Y with weighted sides " + flags);
                Near(x1, expectedX1, "Extreme mixed READ X1 " + flags); Near(x2, expectedX2, "Extreme mixed READ X2 " + flags);
                Near(y1, expectedY1, "Extreme mixed READ Y1 is not clipped " + flags); Near(y2, expectedY2, "Extreme mixed READ Y2 is not clipped " + flags);
                Full(before, Curve(nativeClip), "Extreme READ remains read-only " + flags);
            }
            URflowCurveGuard.ClearAll();
            var nonfinite = Curve(nativeClip); var badKeys = nonfinite.keys; badKeys[0].outTangent = float.PositiveInfinity; nonfinite.keys = badKeys; SetCurve(nativeClip, nonfinite);
            Invoke(state, "ForceRefresh"); Invoke(state, "Refresh"); Invoke(state, "ClearSelections"); SelectNativePair(state);
            var invalidBefore = Curve(nativeClip); float a, b, c, d;
            Require(!URflowReadHelper.ReadFromAnimationWindow(out a, out b, out c, out d), "READ safely rejects an infinite control value");
            Full(invalidBefore, Curve(nativeClip), "Rejected nonfinite READ leaves the clip unchanged");
        }

        private static void SelectNativePair(object state)
        {
            foreach (object candidate in (IEnumerable)Get(state, "allCurves"))
            {
                object binding = Get(candidate, "binding");
                if (!(binding is EditorCurveBinding) || ((EditorCurveBinding)binding).propertyName != X.propertyName) continue;
                var keys = (IList)Get(candidate, "keyframes"); Invoke(state, "SelectKey", keys[0]); Invoke(state, "SelectKey", keys[1]); return;
            }
            throw new InvalidOperationException("Actual native curve missing after READ fixture refresh.");
        }

        private static AnimationCurve Fixture(WeightedMode flags)
        {
            var keys = new[] { new Keyframe(0f, -2f, .3f, 2f), new Keyframe(1f, 0f, 2f, 3f), new Keyframe(2f, 6f, 1f, -3f), new Keyframe(3f, 8f, 2f, -.4f) };
            for (int i = 0; i < keys.Length; i++) { keys[i].inWeight = .14f + i * .09f; keys[i].outWeight = .73f - i * .08f; keys[i].weightedMode = flags; }
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++) { AnimationUtility.SetKeyBroken(curve, i, true); AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free); }
            AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, 2, AnimationUtility.TangentMode.Linear);
            var configured = curve.keys;
            for (int i = 0; i < keys.Length; i++) { configured[i].inWeight = keys[i].inWeight; configured[i].outWeight = keys[i].outWeight; configured[i].weightedMode = flags; }
            curve.keys = configured; return curve;
        }
        private static AnimationClip NewClip(string name) { var clip = new AnimationClip { name = name, legacy = true, hideFlags = HideFlags.DontSave }; Clips.Add(clip); return clip; }
        private static void SetCurve(AnimationClip clip, AnimationCurve curve) { AnimationUtility.SetEditorCurve(clip, X, curve); }
        private static AnimationCurve Curve(AnimationClip clip) { return AnimationUtility.GetEditorCurve(clip, X); }
        private static void TickGuard() { for (int i = 0; i < 12; i++) Invoke(typeof(URflowCurveGuard), "OnEditorUpdate"); }
        private static void Outside(AnimationCurve before, AnimationCurve after, int a, int b, string message)
        {
            Side(before, after, a, true, message + " A.in"); Side(before, after, b, false, message + " B.out");
            for (int i = 0; i < before.length; i++) if (i != a && i != b) FullKey(before, after, i, message + " unselected key " + i);
            if (a > 0) Samples(before, after, before.keys[a - 1].time, before.keys[a].time, message + " left segment");
            if (b + 1 < before.length) Samples(before, after, before.keys[b].time, before.keys[b + 1].time, message + " right segment");
        }
        private static void Side(AnimationCurve before, AnimationCurve after, int i, bool incoming, string message)
        {
            Keyframe a = before.keys[i], b = after.keys[i]; WeightedMode side = incoming ? WeightedMode.In : WeightedMode.Out;
            Near(b.time, a.time, message + " time"); Near(b.value, a.value, message + " value"); Near(incoming ? b.inTangent : b.outTangent, incoming ? a.inTangent : a.outTangent, message + " tangent"); Near(incoming ? b.inWeight : b.outWeight, incoming ? a.inWeight : a.outWeight, message + " weight");
            Require((a.weightedMode & side) == (b.weightedMode & side), message + " weighted flag");
            Require((incoming ? AnimationUtility.GetKeyLeftTangentMode(before, i) : AnimationUtility.GetKeyRightTangentMode(before, i)) == (incoming ? AnimationUtility.GetKeyLeftTangentMode(after, i) : AnimationUtility.GetKeyRightTangentMode(after, i)), message + " tangent mode");
        }
        private static void FullKey(AnimationCurve before, AnimationCurve after, int i, string message) { Side(before, after, i, true, message); Side(before, after, i, false, message); Require(AnimationUtility.GetKeyBroken(before, i) == AnimationUtility.GetKeyBroken(after, i), message + " broken flag"); }
        private static void Full(AnimationCurve before, AnimationCurve after, string message) { Require(before.length == after.length && before.preWrapMode == after.preWrapMode && before.postWrapMode == after.postWrapMode, message + " structure/wrap"); for (int i = 0; i < before.length; i++) FullKey(before, after, i, message + " key " + i); }
        private static void Samples(AnimationCurve before, AnimationCurve after, float a, float b, string message) { for (int i = 0; i <= 32; i++) { float t = Mathf.Lerp(a, b, i / 32f); Near(after.Evaluate(t), before.Evaluate(t), message + " sample " + i); } }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; Report.AppendLine("PASS: " + message); }
        private static void Near(float actual, float expected, string message) { if (actual.Equals(expected) || Mathf.Abs(actual - expected) <= .00001f * Mathf.Max(1f, Mathf.Abs(expected))) return; throw new InvalidOperationException(message + " expected=" + expected + " actual=" + actual); }
        private static object Get(object target, string name) { if (target == null) return null; var p = target.GetType().GetProperty(name, Flags); if (p != null) return p.GetValue(target, null); var f = target.GetType().GetField(name, Flags); return f != null ? f.GetValue(target) : null; }
        private static object Invoke(object target, string name, params object[] args)
        {
            Type type = target as Type ?? target.GetType();
            foreach (MethodInfo method in type.GetMethods(Flags))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length) continue;
                bool match = true; var parameters = method.GetParameters(); for (int i = 0; i < args.Length; i++) if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i])) match = false;
                if (match) return method.Invoke(target is Type ? null : target, args);
            }
            throw new InvalidOperationException("Native method missing: " + type.Name + "." + name);
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= NativeReady;
            URflowCurveGuard.ClearAll();
            if (window != null) window.Close();
            Selection.objects = oldSelection;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            foreach (var clip in Clips) { Undo.ClearUndo(clip); UnityEngine.Object.DestroyImmediate(clip); }
            Report.AppendLine("Assertions=" + checks); Report.AppendLine("Result=" + (error == null ? "PASS" : "FAIL"));
            if (error != null) { Report.AppendLine(error.ToString()); Debug.LogException(error); }
            File.WriteAllText(Evidence, Report.ToString(), Encoding.UTF8); Debug.Log(Report.ToString()); EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
