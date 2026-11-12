using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    /// <summary>
    /// Applies URflow bezier curves to Animation Window keyframes.
    /// Always reads fresh curve data before modifying.
    /// </summary>
    public static class URflowApplyHelper
    {
        private static string _pendingDialog = null;

        public static void CheckPendingDialog()
        {
            if (_pendingDialog != null)
            {
                string msg = _pendingDialog;
                _pendingDialog = null;
                EditorUtility.DisplayDialog("URflow", msg, "OK");
            }
        }

        public static void ApplyToAnimationWindow(float x1, float y1, float x2, float y2, int mode)
        {
            Object aw;
            object state = URflowAnimHelper.GetAnimationState(out aw);
            if (state == null)
            {
                _pendingDialog = "Select a GameObject with an Animator and an AnimationClip.";
                return;
            }

            List<URflowAnimHelper.KeyframeInfo> keyframeInfos = URflowAnimHelper.GetSelectedKeyframes(state);

            if (keyframeInfos.Count == 0)
            {
                _pendingDialog = "Select at least 2 keyframes in the Animation window.\n\nWorks in both Dopesheet and Curves view.";
                return;
            }

            bool anyApplied = false;

            foreach (URflowAnimHelper.KeyframeInfo info in keyframeInfos)
            {
                if (ApplyToClip(info.clip, info.binding, info.selectedIndices, x1, y1, x2, y2, mode))
                    anyApplied = true;
            }

            if (anyApplied)
            {
                EditorWindow awWin = aw as EditorWindow;
                if (awWin != null) awWin.Repaint();
            }
            else
            {
                _pendingDialog = "Could not apply the curve.\nMake sure you select at least 2 keyframes on the same property.";
            }
        }

        internal struct AppliedPair
        {
            internal int IndexA, IndexB;
        }

        // Shared by the Animation-window entry and the independent regression checks.
        internal static bool ApplyToClip(AnimationClip clip, EditorCurveBinding binding,
            IList<int> selectedIndices, float x1, float y1, float x2, float y2, int mode)
        {
            if (clip == null) return false;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            AnimationCurve applied;
            List<AppliedPair> pairs;
            if (!TryBuildAppliedCurve(curve, selectedIndices, x1, y1, x2, y2, mode, out applied, out pairs))
                return false;

            Undo.RecordObject(clip, "URflow Apply Curve");
            AnimationUtility.SetEditorCurve(clip, binding, applied);

            AnimationCurve stored = AnimationUtility.GetEditorCurve(clip, binding);
            if (stored != null)
            {
                Keyframe[] keys = stored.keys;
                foreach (AppliedPair pair in pairs)
                    URflowCurveGuard.Register(clip, binding, pair.IndexA, pair.IndexB,
                        x1, y1, x2, y2, keys[pair.IndexA], keys[pair.IndexB], mode);
            }
            return true;
        }

        internal static bool TryBuildAppliedCurve(AnimationCurve source, IList<int> selectedIndices,
            float x1, float y1, float x2, float y2, int mode,
            out AnimationCurve result, out List<AppliedPair> pairs)
        {
            result = null;
            pairs = new List<AppliedPair>();
            if (source == null || selectedIndices == null || mode < 0 || mode > 2) return false;
            List<int> indices = new List<int>();
            foreach (int index in selectedIndices)
                if (index >= 0 && index < source.length && !indices.Contains(index)) indices.Add(index);
            indices.Sort();
            if (indices.Count < 2) return false;

            Keyframe[] desired = source.keys;
            bool[] changeOut = new bool[desired.Length], changeIn = new bool[desired.Length];
            for (int i = 0; i < indices.Count - 1; i++)
            {
                int a = indices[i], b = indices[i + 1];
                float duration = desired[b].time - desired[a].time;
                if (Mathf.Approximately(duration, 0f)) continue;
                float scale = (desired[b].value - desired[a].value) / duration;
                if (mode == 0 || mode == 1)
                {
                    desired[a].outTangent = Mathf.Approximately(x1, 0f)
                        ? (y1 > 0f ? 1000f : 0f) : y1 / x1 * scale;
                    desired[a].outWeight = x1;
                    desired[a].weightedMode |= WeightedMode.Out;
                    changeOut[a] = true;
                }
                if (mode == 0 || mode == 2)
                {
                    desired[b].inTangent = Mathf.Approximately(x2, 1f)
                        ? (y2 < 1f ? 1000f : 0f) : (1f - y2) / (1f - x2) * scale;
                    desired[b].inWeight = 1f - x2;
                    desired[b].weightedMode |= WeightedMode.In;
                    changeIn[b] = true;
                }
                pairs.Add(new AppliedPair { IndexA = a, IndexB = b });
            }
            if (pairs.Count == 0) return false;

            // Tangent modes belong to individual sides. Breaking a key permits changing
            // one side without turning the unselected side from Auto/Linear into Free.
            AnimationCurve configured = new AnimationCurve(source.keys);
            for (int i = 0; i < desired.Length; i++)
            {
                if (!changeOut[i] && !changeIn[i]) continue;
                AnimationUtility.SetKeyBroken(configured, i, true);
                if (changeIn[i]) AnimationUtility.SetKeyLeftTangentMode(configured, i, AnimationUtility.TangentMode.Free);
                if (changeOut[i]) AnimationUtility.SetKeyRightTangentMode(configured, i, AnimationUtility.TangentMode.Free);
            }

            // Unity's mode setters may recalculate this key or its neighbors. Retain
            // their resulting mode bits, but restore every untouched numeric field.
            Keyframe[] keysWithModes = configured.keys;
            for (int i = 0; i < desired.Length; i++)
            {
                keysWithModes[i].time = desired[i].time;
                keysWithModes[i].value = desired[i].value;
                keysWithModes[i].inTangent = desired[i].inTangent;
                keysWithModes[i].outTangent = desired[i].outTangent;
                keysWithModes[i].inWeight = desired[i].inWeight;
                keysWithModes[i].outWeight = desired[i].outWeight;
                keysWithModes[i].weightedMode = desired[i].weightedMode;
            }
            result = new AnimationCurve(keysWithModes)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
            return true;
        }
    }
}
