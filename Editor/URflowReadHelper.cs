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
            List<int> indices = info.selectedIndices;
            indices.Sort();
            if (indices.Count < 2) return false;

            AnimationCurve freshCurve = info.GetFreshCurve();
            if (freshCurve == null) return false;

            Keyframe[] keyframes = freshCurve.keys;
            int idxA = indices[0];
            int idxB = indices[1];
            if (idxA >= keyframes.Length || idxB >= keyframes.Length) return false;

            Keyframe kA = keyframes[idxA];
            Keyframe kB = keyframes[idxB];

            float dur = kB.time - kA.time;
            float valRange = kB.value - kA.value;

            if (Mathf.Approximately(dur, 0f) || Mathf.Approximately(valRange, 0f))
            {
                x1 = 0f; y1 = 0f; x2 = 1f; y2 = 1f;
                return true;
            }

            float tangentScale = valRange / dur;
            bool aW = (kA.weightedMode == WeightedMode.Out || kA.weightedMode == WeightedMode.Both);
            bool bW = (kB.weightedMode == WeightedMode.In || kB.weightedMode == WeightedMode.Both);

            if (aW && bW)
            {
                x1 = kA.outWeight;
                float sA = Mathf.Approximately(tangentScale, 0f) ? 0f : kA.outTangent / tangentScale;
                y1 = sA * x1;
                x2 = 1f - kB.inWeight;
                float sB = Mathf.Approximately(tangentScale, 0f) ? 0f : kB.inTangent / tangentScale;
                y2 = 1f - sB * (1f - x2);
            }
            else
            {
                float dw = 1f / 3f;
                float sA = Mathf.Approximately(tangentScale, 0f) ? 0f : kA.outTangent / tangentScale;
                x1 = dw; y1 = sA * x1;
                float sB = Mathf.Approximately(tangentScale, 0f) ? 0f : kB.inTangent / tangentScale;
                x2 = 1f - dw; y2 = 1f - sB * (1f - x2);
            }

            x1 = Mathf.Clamp01(x1);
            x2 = Mathf.Clamp01(x2);
            y1 = Mathf.Clamp(y1, -0.5f, 1.5f);
            y2 = Mathf.Clamp(y2, -0.5f, 1.5f);
            return true;
        }
    }
}
