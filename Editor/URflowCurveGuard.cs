using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    /// <summary>
    /// Monitors keyframes that have been assigned URflow bezier curves.
    /// When Unity's Animation Window resets weighted tangent data (e.g. after
    /// dragging a keyframe), this guard automatically re-applies the cached
    /// bezier parameters so the curve shape is preserved.
    /// </summary>
    [InitializeOnLoad]
    public static class URflowCurveGuard
    {
        /// <summary>
        /// Identifies a keyframe pair on a specific curve property.
        /// We track by (clip instance ID, binding path+type, keyframe indices).
        /// </summary>
        private struct PairKey
        {
            public int clipId;
            public string bindingPath;  // binding.path + "|" + binding.propertyName
            public int idxA;
            public int idxB;

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = clipId * 397;
                    h ^= (bindingPath != null ? bindingPath.GetHashCode() : 0);
                    h = h * 397 ^ idxA;
                    h = h * 397 ^ idxB;
                    return h;
                }
            }

            public override bool Equals(object obj)
            {
                if (!(obj is PairKey)) return false;
                PairKey o = (PairKey)obj;
                return clipId == o.clipId && idxA == o.idxA && idxB == o.idxB
                    && bindingPath == o.bindingPath;
            }
        }

        /// <summary>
        /// The bezier parameters and snapshot of the keyframe state at the time
        /// of the last successful apply / re-apply.
        /// </summary>
        private struct CachedCurve
        {
            // Bezier params
            public float x1, y1, x2, y2;

            // Clip + binding so we can re-read fresh data
            public AnimationClip clip;
            public EditorCurveBinding binding;

            // Snapshot of the keyframe values right after apply.
            // Used to detect Unity-side resets vs. intentional user edits.
            public float snapOutTangentA;
            public float snapOutWeightA;
            public WeightedMode snapWeightedModeA;
            public float snapInTangentB;
            public float snapInWeightB;
            public WeightedMode snapWeightedModeB;

            // The value of each keyframe (not time — time changes on drag)
            public float snapValueA;
            public float snapValueB;
        }

        private static readonly Dictionary<PairKey, CachedCurve> _tracked =
            new Dictionary<PairKey, CachedCurve>();

        // Suppress re-entrant checks while we are re-applying
        private static bool _applying = false;

        // Throttle: only check every N editor frames
        private static int _frameCounter = 0;
        private const int CHECK_INTERVAL = 6; // ~10 Hz at 60 fps editor

        static URflowCurveGuard()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        // ───────────────────────────────────────────
        // Public API — called by URflowApplyHelper
        // ───────────────────────────────────────────

        /// <summary>
        /// Register a keyframe pair for automatic curve guarding.
        /// Called right after a successful apply.
        /// </summary>
        public static void Register(
            AnimationClip clip, EditorCurveBinding binding,
            int idxA, int idxB,
            float x1, float y1, float x2, float y2,
            Keyframe kA, Keyframe kB)
        {
            PairKey key = MakeKey(clip, binding, idxA, idxB);
            CachedCurve cc = new CachedCurve
            {
                x1 = x1, y1 = y1, x2 = x2, y2 = y2,
                clip = clip,
                binding = binding,
                snapOutTangentA = kA.outTangent,
                snapOutWeightA = kA.outWeight,
                snapWeightedModeA = kA.weightedMode,
                snapInTangentB = kB.inTangent,
                snapInWeightB = kB.inWeight,
                snapWeightedModeB = kB.weightedMode,
                snapValueA = kA.value,
                snapValueB = kB.value,
            };
            _tracked[key] = cc;
        }

        /// <summary>
        /// Remove all tracked pairs for a given clip + binding.
        /// Useful if the user manually changes the curve to something new.
        /// </summary>
        public static void UnregisterAll(AnimationClip clip, EditorCurveBinding binding)
        {
            string bp = BindingPath(binding);
            int cid = clip.GetInstanceID();
            List<PairKey> toRemove = new List<PairKey>();
            foreach (PairKey pk in _tracked.Keys)
            {
                if (pk.clipId == cid && pk.bindingPath == bp)
                    toRemove.Add(pk);
            }
            for (int i = 0; i < toRemove.Count; i++)
                _tracked.Remove(toRemove[i]);
        }

        /// <summary>
        /// Clear everything (e.g. when clip changes).
        /// </summary>
        public static void ClearAll()
        {
            _tracked.Clear();
        }

        public static int TrackedCount { get { return _tracked.Count; } }

        // ───────────────────────────────────────────
        // Internal tick
        // ───────────────────────────────────────────

        private static void OnEditorUpdate()
        {
            if (_applying || _tracked.Count == 0) return;

            _frameCounter++;
            if (_frameCounter < CHECK_INTERVAL) return;
            _frameCounter = 0;

            // Collect entries that need re-application
            List<PairKey> fixKeys = null;

            foreach (var kvp in _tracked)
            {
                PairKey pk = kvp.Key;
                CachedCurve cc = kvp.Value;

                if (cc.clip == null) continue; // clip was destroyed

                AnimationCurve curve = AnimationUtility.GetEditorCurve(cc.clip, cc.binding);
                if (curve == null || curve.length <= Mathf.Max(pk.idxA, pk.idxB))
                {
                    // Curve gone or indices out of range — schedule removal
                    if (fixKeys == null) fixKeys = new List<PairKey>();
                    fixKeys.Add(pk);
                    continue;
                }

                Keyframe kA = curve.keys[pk.idxA];
                Keyframe kB = curve.keys[pk.idxB];

                // Check: has Unity reset the weighted data?
                // Only trigger on weightedMode reset (Unity strips weighted mode
                // when dragging keyframes). Do NOT trigger on tangent/weight value
                // changes — those are intentional user edits in the Curves view.
                bool resetDetected = false;

                if (!HasWeightedOut(kA.weightedMode) || !HasWeightedIn(kB.weightedMode))
                {
                    resetDetected = true;
                }

                if (resetDetected)
                {
                    if (fixKeys == null) fixKeys = new List<PairKey>();
                    fixKeys.Add(pk);
                }
            }

            if (fixKeys == null) return;

            _applying = true;
            try
            {
                bool anyFixed = false;
                List<PairKey> toRemove = new List<PairKey>();

                for (int f = 0; f < fixKeys.Count; f++)
                {
                    PairKey pk = fixKeys[f];
                    if (!_tracked.ContainsKey(pk)) continue;
                    CachedCurve cc = _tracked[pk];

                    if (cc.clip == null)
                    {
                        toRemove.Add(pk);
                        continue;
                    }

                    AnimationCurve curve = AnimationUtility.GetEditorCurve(cc.clip, cc.binding);
                    if (curve == null || curve.length <= Mathf.Max(pk.idxA, pk.idxB))
                    {
                        toRemove.Add(pk);
                        continue;
                    }

                    Keyframe[] keys = curve.keys;
                    Keyframe kA = keys[pk.idxA];
                    Keyframe kB = keys[pk.idxB];

                    // Recompute tangents based on the NEW time/value positions
                    float dur = kB.time - kA.time;
                    float valRange = kB.value - kA.value;

                    if (Mathf.Approximately(dur, 0f))
                    {
                        toRemove.Add(pk);
                        continue;
                    }

                    float ts = valRange / dur;

                    float outTanA = Mathf.Approximately(cc.x1, 0f)
                        ? (cc.y1 > 0 ? 1000f : 0f)
                        : (cc.y1 / cc.x1) * ts;

                    float inTanB = Mathf.Approximately(cc.x2, 1f)
                        ? (cc.y2 < 1f ? 1000f : 0f)
                        : ((1f - cc.y2) / (1f - cc.x2)) * ts;

                    kA.outTangent = outTanA;
                    kA.outWeight = cc.x1;
                    kA.weightedMode = HasWeightedIn(kA.weightedMode)
                        ? WeightedMode.Both : WeightedMode.Out;

                    kB.inTangent = inTanB;
                    kB.inWeight = 1f - cc.x2;
                    kB.weightedMode = HasWeightedOut(kB.weightedMode)
                        ? WeightedMode.Both : WeightedMode.In;

                    keys[pk.idxA] = kA;
                    keys[pk.idxB] = kB;

                    AnimationCurve newCurve = new AnimationCurve(keys);
                    Undo.RecordObject(cc.clip, "URflow Auto-Restore Curve");
                    AnimationUtility.SetEditorCurve(cc.clip, cc.binding, newCurve);

                    // Update snapshot
                    CachedCurve updated = cc;
                    updated.snapOutTangentA = kA.outTangent;
                    updated.snapOutWeightA = kA.outWeight;
                    updated.snapWeightedModeA = kA.weightedMode;
                    updated.snapInTangentB = kB.inTangent;
                    updated.snapInWeightB = kB.inWeight;
                    updated.snapWeightedModeB = kB.weightedMode;
                    updated.snapValueA = kA.value;
                    updated.snapValueB = kB.value;
                    _tracked[pk] = updated;

                    anyFixed = true;
                }

                // Remove stale entries
                for (int i = 0; i < toRemove.Count; i++)
                    _tracked.Remove(toRemove[i]);

                if (anyFixed)
                {
                    // Repaint Animation Window
                    System.Type awType = System.Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
                    if (awType != null)
                    {
                        Object[] windows = Resources.FindObjectsOfTypeAll(awType);
                        for (int i = 0; i < windows.Length; i++)
                        {
                            EditorWindow w = windows[i] as EditorWindow;
                            if (w != null) w.Repaint();
                        }
                    }
                }
            }
            finally
            {
                _applying = false;
            }
        }

        // ───────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────

        private static PairKey MakeKey(AnimationClip clip, EditorCurveBinding binding, int idxA, int idxB)
        {
            return new PairKey
            {
                clipId = clip.GetInstanceID(),
                bindingPath = BindingPath(binding),
                idxA = idxA,
                idxB = idxB,
            };
        }

        private static string BindingPath(EditorCurveBinding b)
        {
            return b.path + "|" + b.propertyName + "|" + (b.type != null ? b.type.FullName : "");
        }

        private static bool HasWeightedOut(WeightedMode m)
        {
            return m == WeightedMode.Out || m == WeightedMode.Both;
        }

        private static bool HasWeightedIn(WeightedMode m)
        {
            return m == WeightedMode.In || m == WeightedMode.Both;
        }
    }
}
