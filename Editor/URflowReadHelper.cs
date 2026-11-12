using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    public static class URflowReadHelper
    {
        public static bool ReadFromAnimationWindow(out float x1, out float y1, out float x2, out float y2)
        {
            x1 = 0.25f; y1 = 0.1f; x2 = 0.25f; y2 = 1f;

            Object aw;
            object state = URflowAnimHelper.GetAnimationState(out aw);
            if (state == null) return false;

            List<URflowAnimHelper.KeyframeInfo> infos = URflowAnimHelper.GetSelectedKeyframes(state);
            if (infos.Count == 0) return false;

            URflowAnimHelper.KeyframeInfo info = infos[0];
            List<int> indices = new List<int>(info.selectedIndices);
            indices.Sort();
            if (indices.Count < 2) return false;

            AnimationCurve freshCurve = info.GetFreshCurve();
            if (freshCurve == null) return false;

            Keyframe[] keyframes = freshCurve.keys;
            int idxA = indices[0];
            int idxB = indices[1];
            if (idxA < 0 || idxB < 0 || idxA >= keyframes.Length || idxB >= keyframes.Length) return false;

            Keyframe kA = keyframes[idxA];
            Keyframe kB = keyframes[idxB];

            float dur = kB.time - kA.time;
            float valRange = kB.value - kA.value;
            if (!IsFinite(dur) || !IsFinite(valRange)) return false;

            if (Mathf.Approximately(dur, 0f) || Mathf.Approximately(valRange, 0f))
            {
                x1 = 0f; y1 = 0f; x2 = 1f; y2 = 1f;
                return true;
            }

            float tangentScale = valRange / dur;
            bool aW = (kA.weightedMode == WeightedMode.Out || kA.weightedMode == WeightedMode.Both);
            bool bW = (kB.weightedMode == WeightedMode.In || kB.weightedMode == WeightedMode.Both);

            // Each tangent side can be weighted independently. The unweighted side
            // uses the Hermite-equivalent third, without discarding the other weight.
            x1 = aW ? kA.outWeight : 1f / 3f;
            x2 = 1f - (bW ? kB.inWeight : 1f / 3f);
            y1 = kA.outTangent / tangentScale * x1;
            y2 = 1f - kB.inTangent / tangentScale * (1f - x2);
            if (!IsFinite(x1) || !IsFinite(y1) || !IsFinite(x2) || !IsFinite(y2)) return false;

            x1 = Mathf.Clamp01(x1);
            x2 = Mathf.Clamp01(x2);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
