using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    /// <summary>
    /// Monitors keyframes that have been assigned URflow bezier curves.
    /// Restores a lost weighted flag only for an unchanged tangent on a moved key.
    /// Manual tangent, weight or mode edits release protection of that direction.
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
            public bool guardOut, guardIn;
            public int keyCount;

            // Latest observed state, rather than the original APPLY state. The
            // full ordered key sequence lets us reject ambiguous multi-key moves
            // and reordering without assigning old protection to another key.
            public Keyframe[] snapshot;
            public AnimationUtility.TangentMode snapOutModeA, snapInModeB;
            public bool snapBrokenA, snapBrokenB;
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
            // An Undo may legitimately remove the weighted flag. Never restore the
            // stale apply snapshot over the state the user has just undone/redone.
            Undo.undoRedoPerformed += ClearAll;
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
            Keyframe kA, Keyframe kB, int mode = 0)
        {
            if (clip == null || idxA < 0 || idxB <= idxA || mode < 0 || mode > 2) return;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || idxB >= curve.length) return;
            PairKey key = MakeKey(clip, binding, idxA, idxB);
            bool ownsOut = mode == 0 || mode == 1, ownsIn = mode == 0 || mode == 2;

            // A newly applied side supersedes older protection of that same side.
            // Adjacent pairs own opposite sides of their shared key and remain valid.
            List<PairKey> existing = new List<PairKey>(_tracked.Keys);
            foreach (PairKey oldKey in existing)
            {
                if (oldKey.Equals(key) || oldKey.clipId != key.clipId || oldKey.bindingPath != key.bindingPath) continue;
                CachedCurve old = _tracked[oldKey];
                if (ownsOut && oldKey.idxA == idxA) old.guardOut = false;
                if (ownsIn && oldKey.idxB == idxB) old.guardIn = false;
                if (!old.guardOut && !old.guardIn) _tracked.Remove(oldKey);
                else _tracked[oldKey] = old;
            }

            CachedCurve cc;
            _tracked.TryGetValue(key, out cc);
            if (ownsOut) { cc.x1 = x1; cc.y1 = y1; cc.guardOut = true; }
            if (ownsIn) { cc.x2 = x2; cc.y2 = y2; cc.guardIn = true; }
            cc.clip = clip;
            cc.binding = binding;
            CaptureSnapshot(ref cc, curve, curve.keys, key);
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
            _frameCounter = 0;
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

            // Updating or releasing individual sides must not mutate an active
            // dictionary enumerator. Adjacent pairs retain their own directions.
            List<PairKey> observed = new List<PairKey>(_tracked.Keys);
            foreach (PairKey pk in observed)
            {
                CachedCurve cc;
                if (!_tracked.TryGetValue(pk, out cc)) continue;

                AnimationCurve curve = cc.clip != null ? AnimationUtility.GetEditorCurve(cc.clip, cc.binding) : null;
                if (curve == null || curve.length != cc.keyCount || curve.length <= pk.idxB
                    || cc.snapshot == null || cc.snapshot.Length != curve.length)
                {
                    _tracked.Remove(pk);
                    continue;
                }

                Keyframe[] keys = curve.keys;
                if (PositionChangeCount(cc.snapshot, keys) > 1)
                {
                    // Unity has no stable key ID. Group moves, reordered keys and
                    // replacements are ambiguous, so leave the edited curve alone.
                    _tracked.Remove(pk);
                    continue;
                }
                Keyframe kA = keys[pk.idxA], kB = keys[pk.idxB];
                Keyframe beforeA = cc.snapshot[pk.idxA], beforeB = cc.snapshot[pk.idxB];
                bool movedA = PositionChanged(beforeA, kA), movedB = PositionChanged(beforeB, kB);

                // Tangent/weight/mode changes are edits, even if weighted mode was
                // also cleared. Broken couples both sides of the key; each owner
                // therefore releases its direction when that key-level state changes.
                if (cc.guardOut && (beforeA.outTangent != kA.outTangent
                    || beforeA.outWeight != kA.outWeight
                    || cc.snapOutModeA != AnimationUtility.GetKeyRightTangentMode(curve, pk.idxA)
                    || cc.snapBrokenA != AnimationUtility.GetKeyBroken(curve, pk.idxA)))
                    cc.guardOut = false;
                if (cc.guardIn && (beforeB.inTangent != kB.inTangent
                    || beforeB.inWeight != kB.inWeight
                    || cc.snapInModeB != AnimationUtility.GetKeyLeftTangentMode(curve, pk.idxB)
                    || cc.snapBrokenB != AnimationUtility.GetKeyBroken(curve, pk.idxB)))
                    cc.guardIn = false;

                // Disabling Weighted at a stationary key is a deliberate edit.
                // Never treat it as a request to resurrect the old APPLY cache.
                if (!movedA && cc.guardOut && !HasWeightedOut(kA.weightedMode)) cc.guardOut = false;
                if (!movedB && cc.guardIn && !HasWeightedIn(kB.weightedMode)) cc.guardIn = false;
                if (!cc.guardOut && !cc.guardIn)
                {
                    _tracked.Remove(pk);
                    continue;
                }
                bool resetDetected = (cc.guardOut && !HasWeightedOut(kA.weightedMode))
                    || (cc.guardIn && !HasWeightedIn(kB.weightedMode));
                // Observe every move. A later stationary flag change must not be
                // classified against the key positions from the original APPLY.
                CaptureSnapshot(ref cc, curve, keys, pk);
                _tracked[pk] = cc;
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
                    if (curve == null || curve.length != cc.keyCount || curve.length <= Mathf.Max(pk.idxA, pk.idxB))
                    {
                        toRemove.Add(pk);
                        continue;
                    }

                    Keyframe[] keys = curve.keys;
                    Keyframe kA = keys[pk.idxA];
                    Keyframe kB = keys[pk.idxB];

                    bool restoreOut = cc.guardOut && !HasWeightedOut(kA.weightedMode);
                    bool restoreIn = cc.guardIn && !HasWeightedIn(kB.weightedMode);
                    if (!restoreOut && !restoreIn) continue;
                    int restoreMode = restoreOut ? (restoreIn ? 0 : 1) : 2;
                    AnimationCurve newCurve;
                    List<URflowApplyHelper.AppliedPair> restoredPairs;
                    if (!URflowApplyHelper.TryBuildAppliedCurve(curve, new[] { pk.idxA, pk.idxB },
                        cc.x1, cc.y1, cc.x2, cc.y2, restoreMode, out newCurve, out restoredPairs))
                    {
                        toRemove.Add(pk);
                        continue;
                    }
                    Undo.RecordObject(cc.clip, "URflow Auto-Restore Curve");
                    AnimationUtility.SetEditorCurve(cc.clip, cc.binding, newCurve);

                    AnimationCurve stored = AnimationUtility.GetEditorCurve(cc.clip, cc.binding);
                    if (stored == null) { toRemove.Add(pk); continue; }
                    kA = stored.keys[pk.idxA];
                    kB = stored.keys[pk.idxB];

                    CachedCurve updated = cc;
                    CaptureSnapshot(ref updated, stored, stored.keys, pk);
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

        private static void CaptureSnapshot(ref CachedCurve cc, AnimationCurve curve, Keyframe[] keys, PairKey key)
        {
            cc.keyCount = keys.Length;
            cc.snapshot = keys;
            cc.snapOutModeA = AnimationUtility.GetKeyRightTangentMode(curve, key.idxA);
            cc.snapInModeB = AnimationUtility.GetKeyLeftTangentMode(curve, key.idxB);
            cc.snapBrokenA = AnimationUtility.GetKeyBroken(curve, key.idxA);
            cc.snapBrokenB = AnimationUtility.GetKeyBroken(curve, key.idxB);
        }

        private static bool PositionChanged(Keyframe before, Keyframe after)
        {
            return before.time != after.time || before.value != after.value;
        }

        private static int PositionChangeCount(Keyframe[] before, Keyframe[] after)
        {
            int count = 0;
            for (int i = 0; i < before.Length; i++)
                if (PositionChanged(before[i], after[i]) && ++count > 1) break;
            return count;
        }

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
