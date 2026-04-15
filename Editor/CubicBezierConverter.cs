using UnityEngine;

namespace URflow
{
    /// <summary>
    /// Converts CSS cubic-bezier(x1, y1, x2, y2) parameters
    /// into Unity AnimationCurve with proper tangents.
    /// 
    /// CSS cubic-bezier defines a curve from (0,0) to (1,1)
    /// with two control points P1(x1,y1) and P2(x2,y2).
    /// 
    /// Unity AnimationCurve uses keyframes with inTangent/outTangent slopes.
    /// </summary>
    public static class CubicBezierConverter
    {
        /// <summary>
        /// Convert cubic-bezier parameters to a Unity AnimationCurve.
        /// The curve maps time [0..duration] to value [startValue..endValue].
        /// </summary>
        public static AnimationCurve ToCurve(
            float x1, float y1, float x2, float y2,
            float duration = 1f,
            float startValue = 0f,
            float endValue = 1f)
        {
            // For a cubic bezier B(t) with control points:
            //   P0 = (0, 0)
            //   P1 = (x1, y1)
            //   P2 = (x2, y2)
            //   P3 = (1, 1)
            //
            // The derivative at P0 (t=0): B'(0) = 3*(P1 - P0) = 3*(x1, y1)
            //   slope at start = dy/dx = (3*y1) / (3*x1) = y1/x1
            //
            // The derivative at P3 (t=1): B'(1) = 3*(P3 - P2) = 3*(1-x2, 1-y2)
            //   slope at end = dy/dx = (1-y2) / (1-x2)

            float valueRange = endValue - startValue;

            // Calculate tangent slopes in normalized space
            float outTangentStart; // tangent leaving the first keyframe
            float inTangentEnd;   // tangent entering the last keyframe

            // Handle edge cases for vertical/horizontal tangents
            if (Mathf.Approximately(x1, 0f))
                outTangentStart = y1 > 0f ? float.PositiveInfinity : 0f;
            else
                outTangentStart = y1 / x1;

            if (Mathf.Approximately(x2, 1f))
                inTangentEnd = y2 < 1f ? float.PositiveInfinity : 0f;
            else
                inTangentEnd = (1f - y2) / (1f - x2);

            // Scale tangents to account for actual time/value ranges
            // Unity tangent = dy/dt in real units
            // We have slope in normalized [0..1] space, need to scale:
            //   realTangent = normalizedSlope * (valueRange / duration)
            float tangentScale = valueRange / duration;
            outTangentStart *= tangentScale;
            inTangentEnd *= tangentScale;

            var keyStart = new Keyframe(0f, startValue)
            {
                outTangent = outTangentStart,
                inTangent = 0f,
                outWeight = x1,          // Bezier weight = control point x
                inWeight = 0f,
                weightedMode = WeightedMode.Out
            };

            var keyEnd = new Keyframe(duration, endValue)
            {
                inTangent = inTangentEnd,
                outTangent = 0f,
                inWeight = 1f - x2,       // Bezier weight from the end
                outWeight = 0f,
                weightedMode = WeightedMode.In
            };

            var curve = new AnimationCurve(keyStart, keyEnd);
            return curve;
        }

        /// <summary>
        /// Create a curve from a BezierPreset.
        /// </summary>
        public static AnimationCurve ToCurve(BezierPreset preset,
            float duration = 1f, float startValue = 0f, float endValue = 1f)
        {
            return ToCurve(preset.x1, preset.y1, preset.x2, preset.y2,
                duration, startValue, endValue);
        }

        /// <summary>
        /// Extract cubic-bezier parameters from a 2-keyframe weighted AnimationCurve.
        /// Returns false if the curve cannot be represented as a simple cubic-bezier.
        /// </summary>
        public static bool FromCurve(AnimationCurve curve,
            out float x1, out float y1, out float x2, out float y2)
        {
            x1 = y1 = x2 = y2 = 0f;

            if (curve == null || curve.length != 2)
                return false;

            var k0 = curve.keys[0];
            var k1 = curve.keys[1];

            float duration = k1.time - k0.time;
            float valueRange = k1.value - k0.value;

            if (Mathf.Approximately(duration, 0f) || Mathf.Approximately(valueRange, 0f))
                return false;

            float tangentScale = valueRange / duration;

            // Recover x1 from outWeight of first key
            x1 = k0.outWeight;

            // Recover y1 from tangent: y1 = slope_normalized * x1
            float slopeNorm = Mathf.Approximately(tangentScale, 0f)
                ? 0f
                : k0.outTangent / tangentScale;
            y1 = slopeNorm * x1;

            // Recover x2: x2 = 1 - inWeight of last key
            x2 = 1f - k1.inWeight;

            // Recover y2 from tangent: (1-y2)/(1-x2) = slope_normalized
            float slopeNormEnd = Mathf.Approximately(tangentScale, 0f)
                ? 0f
                : k1.inTangent / tangentScale;
            float oneMinusX2 = 1f - x2;
            y2 = 1f - slopeNormEnd * oneMinusX2;

            return true;
        }

        /// <summary>
        /// Sample the cubic-bezier curve at a given normalized time (0..1).
        /// Uses De Casteljau's algorithm for accurate evaluation.
        /// Returns the normalized Y value.
        /// </summary>
        public static float Evaluate(float x1, float y1, float x2, float y2, float t)
        {
            // Find the bezier parameter 'u' that corresponds to time 't'
            // by solving B_x(u) = t using Newton-Raphson
            float u = t; // initial guess

            for (int i = 0; i < 8; i++)
            {
                float bx = BezierComponent(0f, x1, x2, 1f, u);
                float diff = bx - t;
                if (Mathf.Abs(diff) < 1e-6f)
                    break;

                float dbx = BezierDerivative(0f, x1, x2, 1f, u);
                if (Mathf.Approximately(dbx, 0f))
                    break;

                u -= diff / dbx;
                u = Mathf.Clamp01(u);
            }

            return BezierComponent(0f, y1, y2, 1f, u);
        }

        /// <summary>
        /// Evaluate one component of a cubic bezier at parameter u.
        /// B(u) = (1-u)^3*p0 + 3*(1-u)^2*u*p1 + 3*(1-u)*u^2*p2 + u^3*p3
        /// </summary>
        private static float BezierComponent(float p0, float p1, float p2, float p3, float u)
        {
            float oneMinusU = 1f - u;
            return oneMinusU * oneMinusU * oneMinusU * p0
                 + 3f * oneMinusU * oneMinusU * u * p1
                 + 3f * oneMinusU * u * u * p2
                 + u * u * u * p3;
        }

        /// <summary>
        /// Derivative of one component of a cubic bezier at parameter u.
        /// B'(u) = 3*(1-u)^2*(p1-p0) + 6*(1-u)*u*(p2-p1) + 3*u^2*(p3-p2)
        /// </summary>
        private static float BezierDerivative(float p0, float p1, float p2, float p3, float u)
        {
            float oneMinusU = 1f - u;
            return 3f * oneMinusU * oneMinusU * (p1 - p0)
                 + 6f * oneMinusU * u * (p2 - p1)
                 + 3f * u * u * (p3 - p2);
        }
    }
}
