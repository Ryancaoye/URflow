using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    public static class URflowAnimHelper
    {
        private const BindingFlags ALL = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public class KeyframeInfo
        {
            public AnimationClip clip;
            public EditorCurveBinding binding;
            public List<int> selectedIndices = new List<int>();

            public AnimationCurve GetFreshCurve()
            {
                return AnimationUtility.GetEditorCurve(clip, binding);
            }
        }

        public static object GetAnimationState(out Object animWindow)
        {
            animWindow = null;
            System.Type awType = System.Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
            if (awType == null) return null;

            Object[] windows = Resources.FindObjectsOfTypeAll(awType);
            if (windows.Length == 0) return null;
            animWindow = windows[0];

            FieldInfo aeField = awType.GetField("m_AnimEditor", ALL);
            if (aeField == null) return null;
            object ae = aeField.GetValue(animWindow);
            if (ae == null) return null;

            object state = GetMember(ae, "state");
            if (state == null) state = GetFieldValue(ae, "m_State");
            return state;
        }

        /// <summary>
        /// Get the AnimEditor object from the Animation Window.
        /// </summary>
        private static object GetAnimEditor(Object animWindow)
        {
            if (animWindow == null) return null;
            FieldInfo aeField = animWindow.GetType().GetField("m_AnimEditor", ALL);
            return aeField != null ? aeField.GetValue(animWindow) : null;
        }

        public static List<KeyframeInfo> GetSelectedKeyframes(object state)
        {
            return GetSelectedKeyframes(state, false);
        }

        public static List<KeyframeInfo> GetSelectedKeyframes(object state, bool debug)
        {
            List<KeyframeInfo> result = new List<KeyframeInfo>();
            if (state == null) return result;

            // ── Approach 1: selectedKeys (Dopesheet mode) ──
            object selKeysObj = GetMember(state, "selectedKeys");
            System.Collections.IList selectedKeys = selKeysObj as System.Collections.IList;

            if (selectedKeys != null && selectedKeys.Count >= 2)
            {
                result = ResolveFromSelectedKeys(selectedKeys);
                if (result.Count > 0) return result;
            }

            // ── Approach 2: CurveEditor.selectedCurves (Curves view) ──
            result = ResolveFromCurveEditor(state);
            return result;
        }

        // ═══════════════════════════════════════════════
        // Dopesheet mode: selectedKeys
        // ═══════════════════════════════════════════════

        private static List<KeyframeInfo> ResolveFromSelectedKeys(System.Collections.IList selectedKeys)
        {
            List<KeyframeInfo> result = new List<KeyframeInfo>();
            Dictionary<int, KeyframeInfo> byHash = new Dictionary<int, KeyframeInfo>();

            for (int i = 0; i < selectedKeys.Count; i++)
            {
                object key = selectedKeys[i];

                object parentCurve = GetFieldValue(key, "m_Curve");
                if (parentCurve == null) parentCurve = GetMember(key, "curve");
                if (parentCurve == null) continue;

                float keyTime = GetKeyTime(key);
                if (keyTime < 0f) continue;

                int hash = parentCurve.GetHashCode();
                if (!byHash.ContainsKey(hash))
                {
                    KeyframeInfo info = ResolveKeyframeInfoFromAwCurve(parentCurve);
                    if (info == null) continue;
                    byHash[hash] = info;
                    result.Add(info);
                }

                KeyframeInfo kfInfo = byHash[hash];
                AnimationCurve freshCurve = kfInfo.GetFreshCurve();
                if (freshCurve == null) continue;

                int idx = FindIndexByTime(freshCurve, keyTime);
                if (idx >= 0 && !kfInfo.selectedIndices.Contains(idx))
                    kfInfo.selectedIndices.Add(idx);
            }

            result.RemoveAll(delegate(KeyframeInfo k) { return k.selectedIndices.Count < 2; });
            return result;
        }

        // ═══════════════════════════════════════════════
        // Curves view: CurveEditor → selectedCurves + animationCurves
        // ═══════════════════════════════════════════════

        private static List<KeyframeInfo> ResolveFromCurveEditor(object state)
        {
            List<KeyframeInfo> result = new List<KeyframeInfo>();

            // Get AnimEditor → m_CurveEditor
            System.Type awType = System.Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
            if (awType == null) return result;

            Object[] windows = Resources.FindObjectsOfTypeAll(awType);
            if (windows.Length == 0) return result;

            object ae = GetFieldValue(windows[0], "m_AnimEditor");
            if (ae == null) return result;

            object ce = GetFieldValue(ae, "m_CurveEditor");
            if (ce == null) return result;

            // Get selectedCurves (List<CurveSelection>)
            object scObj = GetMember(ce, "selectedCurves");
            System.Collections.IList scList = scObj as System.Collections.IList;
            if (scList == null || scList.Count < 2) return result;

            // Get animationCurves (CurveWrapper[])
            object acObj = GetMember(ce, "animationCurves");
            System.Array acArr = acObj as System.Array;
            if (acArr == null || acArr.Length == 0) return result;

            // Group CurveSelection by curveID → List<key index>
            Dictionary<int, List<int>> byCurveId = new Dictionary<int, List<int>>();
            for (int i = 0; i < scList.Count; i++)
            {
                object cs = scList[i];
                object curveIdObj = GetMember(cs, "curveID");
                object keyObj = GetMember(cs, "key");
                if (curveIdObj == null || keyObj == null) continue;

                int curveId = (int)curveIdObj;
                int keyIdx = (int)keyObj;

                if (!byCurveId.ContainsKey(curveId))
                    byCurveId[curveId] = new List<int>();
                if (!byCurveId[curveId].Contains(keyIdx))
                    byCurveId[curveId].Add(keyIdx);
            }

            // Match curveID → CurveWrapper (by id field)
            for (int i = 0; i < acArr.Length; i++)
            {
                object cw = acArr.GetValue(i);
                if (cw == null) continue;

                object idObj = GetFieldValue(cw, "id");
                if (idObj == null) continue;
                int cwId = (int)idObj;

                if (!byCurveId.ContainsKey(cwId)) continue;
                List<int> indices = byCurveId[cwId];
                if (indices.Count < 2) continue;

                // Get binding and clip from CurveWrapper
                object bindingObj = GetFieldValue(cw, "binding");
                object clipObj = GetMember(cw, "animationClip");

                if (!(bindingObj is EditorCurveBinding) || !(clipObj is AnimationClip))
                    continue;

                KeyframeInfo info = new KeyframeInfo();
                info.clip = (AnimationClip)clipObj;
                info.binding = (EditorCurveBinding)bindingObj;
                info.selectedIndices = indices;
                info.selectedIndices.Sort();
                result.Add(info);
            }

            return result;
        }

        // ═══════════════════════════════════════════════
        // Shared helpers
        // ═══════════════════════════════════════════════

        /// <summary>
        /// Resolve KeyframeInfo from an AnimationWindowCurve (Dopesheet mode).
        /// </summary>
        public static KeyframeInfo ResolveKeyframeInfoFromAwCurve(object awCurve)
        {
            if (awCurve == null) return null;

            object bindingObj = GetMember(awCurve, "binding");
            object clipObj = GetMember(awCurve, "clip");
            if (bindingObj == null) bindingObj = GetFieldValue(awCurve, "m_Binding");
            if (clipObj == null) clipObj = GetFieldValue(awCurve, "m_Clip");

            if (!(bindingObj is EditorCurveBinding) || !(clipObj is AnimationClip))
                return null;

            KeyframeInfo info = new KeyframeInfo();
            info.clip = (AnimationClip)clipObj;
            info.binding = (EditorCurveBinding)bindingObj;
            return info;
        }

        public static float GetKeyTime(object key)
        {
            object timeObj = GetMember(key, "time");
            if (timeObj != null) return (float)timeObj;
            object mTimeObj = GetFieldValue(key, "m_Time");
            if (mTimeObj != null) return (float)mTimeObj;
            return -1f;
        }

        public static int FindIndexByTime(AnimationCurve curve, float time)
        {
            int bestIdx = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < curve.length; i++)
            {
                float dist = Mathf.Abs(curve.keys[i].time - time);
                if (dist < bestDist) { bestDist = dist; bestIdx = i; }
            }
            return (bestIdx >= 0 && bestDist < 0.001f) ? bestIdx : -1;
        }

        public static object GetMember(object obj, string name)
        {
            if (obj == null) return null;
            System.Type t = obj.GetType();
            PropertyInfo prop = t.GetProperty(name, ALL);
            if (prop != null)
            {
                try { return prop.GetValue(obj, null); }
                catch { return null; }
            }
            FieldInfo field = t.GetField(name, ALL);
            if (field != null)
            {
                try { return field.GetValue(obj); }
                catch { return null; }
            }
            return null;
        }

        public static object GetFieldValue(object obj, string name)
        {
            if (obj == null) return null;
            FieldInfo field = obj.GetType().GetField(name, ALL);
            if (field != null)
            {
                try { return field.GetValue(obj); }
                catch { return null; }
            }
            return null;
        }
    }
}
