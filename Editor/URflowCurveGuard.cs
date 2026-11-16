using UnityEditor;
using UnityEngine;

namespace URflow
{
    /// <summary>
    /// Compatibility entry points for the retired automatic curve guard.
    /// APPLY writes once; later edits and weighted behavior are handled by Unity.
    /// No editor callbacks are registered and no animation data is read or written.
    /// </summary>
    public static class URflowCurveGuard
    {
        public static void Register(
            AnimationClip clip, EditorCurveBinding binding,
            int idxA, int idxB,
            float x1, float y1, float x2, float y2,
            Keyframe kA, Keyframe kB, int mode = 0)
        {
        }

        public static void UnregisterAll(AnimationClip clip, EditorCurveBinding binding)
        {
        }

        public static void ClearAll()
        {
        }

        public static int TrackedCount { get { return 0; } }

    }
}
