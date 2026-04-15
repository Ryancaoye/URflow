using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    public static class URflowWeightedHelper
    {
        public static void SetSelectedKeysWeighted()
        {
            Object aw;
            object state = URflowAnimHelper.GetAnimationState(out aw);
            if (state == null) return;

            List<URflowAnimHelper.KeyframeInfo> infos = URflowAnimHelper.GetSelectedKeyframes(state);
            if (infos.Count == 0) return;

            int total = 0;

            foreach (URflowAnimHelper.KeyframeInfo info in infos)
            {
                AnimationCurve freshCurve = info.GetFreshCurve();
                if (freshCurve == null) continue;

                Keyframe[] keyframes = freshCurve.keys;
                bool modified = false;

                foreach (int idx in info.selectedIndices)
                {
                    if (idx >= keyframes.Length) continue;
                    Keyframe kf = keyframes[idx];
                    if (kf.weightedMode != WeightedMode.Both)
                    {
                        kf.weightedMode = WeightedMode.Both;
                        if (Mathf.Approximately(kf.inWeight, 0f)) kf.inWeight = 1f / 3f;
                        if (Mathf.Approximately(kf.outWeight, 0f)) kf.outWeight = 1f / 3f;
                        keyframes[idx] = kf;
                        modified = true;
                        total++;
                    }
                }

                if (!modified) continue;
                AnimationCurve newCurve = new AnimationCurve(keyframes);
                Undo.RecordObject(info.clip, "URflow Set Weighted");
                AnimationUtility.SetEditorCurve(info.clip, info.binding, newCurve);
            }

            if (total > 0)
            {
                EditorWindow awWin = aw as EditorWindow;
                if (awWin != null) awWin.Repaint();
            }
        }
    }
}
