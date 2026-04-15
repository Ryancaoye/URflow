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
                List<int> indices = info.selectedIndices;
                indices.Sort();

                // IMPORTANT: Get FRESH curve data from the clip, not a stale snapshot
                AnimationCurve freshCurve = info.GetFreshCurve();
                if (freshCurve == null) continue;

                Keyframe[] keyframes = freshCurve.keys;
                bool modified = false;

                for (int i = 0; i < indices.Count - 1; i++)
                {
                    int idxA = indices[i];
                    int idxB = indices[i + 1];
                    if (idxA >= keyframes.Length || idxB >= keyframes.Length) continue;

                    Keyframe kA = keyframes[idxA];
                    Keyframe kB = keyframes[idxB];

                    float dur = kB.time - kA.time;
                    float valRange = kB.value - kA.value;
                    if (Mathf.Approximately(dur, 0f)) continue;

                    float ts = valRange / dur;

                    float outTanA = Mathf.Approximately(x1, 0f)
                        ? (y1 > 0 ? 1000f : 0f)
                        : (y1 / x1) * ts;

                    float inTanB = Mathf.Approximately(x2, 1f)
                        ? (y2 < 1f ? 1000f : 0f)
                        : ((1f - y2) / (1f - x2)) * ts;

                    if (mode == 0 || mode == 1)
                    {
                        kA.outTangent = outTanA;
                        kA.outWeight = x1;
                        kA.weightedMode = (kA.weightedMode == WeightedMode.In)
                            ? WeightedMode.Both : WeightedMode.Out;
                    }
                    if (mode == 0 || mode == 2)
                    {
                        kB.inTangent = inTanB;
                        kB.inWeight = 1f - x2;
                        kB.weightedMode = (kB.weightedMode == WeightedMode.Out)
                            ? WeightedMode.Both : WeightedMode.In;
                    }

                    keyframes[idxA] = kA;
                    keyframes[idxB] = kB;
                    modified = true;
                }

                if (!modified) continue;

                AnimationCurve newCurve = new AnimationCurve(keyframes);
                Undo.RecordObject(info.clip, "URflow Apply Curve");
                AnimationUtility.SetEditorCurve(info.clip, info.binding, newCurve);

                // Re-read the curve Unity actually stored and update guard snapshots
                AnimationCurve storedCurve = AnimationUtility.GetEditorCurve(info.clip, info.binding);
                if (storedCurve != null)
                {
                    for (int i = 0; i < indices.Count - 1; i++)
                    {
                        int idxA = indices[i];
                        int idxB = indices[i + 1];
                        if (idxA < storedCurve.length && idxB < storedCurve.length)
                        {
                            URflowCurveGuard.Register(
                                info.clip, info.binding, idxA, idxB,
                                x1, y1, x2, y2,
                                storedCurve.keys[idxA], storedCurve.keys[idxB]);
                        }
                    }
                }

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
    }
}
