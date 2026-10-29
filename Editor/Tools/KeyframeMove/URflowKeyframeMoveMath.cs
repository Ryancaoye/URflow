using System;
using UnityEngine;

namespace URflow
{
    public enum URflowKeyframeMoveAnchor
    {
        First,
        Last
    }

    public enum URflowKeyframeMoveMode
    {
        Translate,
        Proportional
    }

    public sealed class URflowKeyframeMoveMathResult
    {
        public AnimationCurve Curve { get; private set; }
        public float OriginalAnchorValue { get; private set; }
        public float TargetValue { get; private set; }
        public float DeltaOrFactor { get; private set; }
        public bool HasChanges { get; private set; }
        public bool IsZeroFactor { get; private set; }
        public bool IsNegativeFactor { get; private set; }
        public URflowKeyframeMoveAnchor Anchor { get; private set; }
        public URflowKeyframeMoveMode Mode { get; private set; }
        public string Warning { get; private set; }

        internal URflowKeyframeMoveMathResult(
            AnimationCurve curve, float originalAnchorValue, float targetValue,
            float deltaOrFactor, bool hasChanges,
            URflowKeyframeMoveAnchor anchor, URflowKeyframeMoveMode mode)
        {
            Curve = curve;
            OriginalAnchorValue = originalAnchorValue;
            TargetValue = targetValue;
            DeltaOrFactor = deltaOrFactor;
            HasChanges = hasChanges;
            Anchor = anchor;
            Mode = mode;
            IsZeroFactor = mode == URflowKeyframeMoveMode.Proportional && deltaOrFactor == 0f;
            IsNegativeFactor = mode == URflowKeyframeMoveMode.Proportional && deltaOrFactor < 0f;
            Warning = IsZeroFactor
                ? "A zero factor makes this property's entire curve zero."
                : IsNegativeFactor
                    ? "A negative factor reverses the property's values and finite tangent slopes."
                    : string.Empty;
        }
    }

    /// <summary>
    /// Builds a complete curve transformation without changing the source curve.
    /// Unity asset writes, preview state and Undo belong to the service layer.
    /// </summary>
    public static class URflowKeyframeMoveMath
    {
        public static bool TryBuildCurve(
            AnimationCurve source, float targetValue,
            URflowKeyframeMoveAnchor anchor, URflowKeyframeMoveMode mode,
            out URflowKeyframeMoveMathResult result, out string error)
        {
            result = null;
            error = null;

            if (source == null || source.length == 0)
            {
                error = "The selected curve has no keyframes.";
                return false;
            }
            if (!IsFinite(targetValue))
            {
                error = "The static default value must be finite.";
                return false;
            }
            if (anchor != URflowKeyframeMoveAnchor.First && anchor != URflowKeyframeMoveAnchor.Last)
            {
                error = "The alignment anchor is not supported.";
                return false;
            }
            if (mode != URflowKeyframeMoveMode.Translate && mode != URflowKeyframeMoveMode.Proportional)
            {
                error = "The keyframe movement mode is not supported.";
                return false;
            }

            Keyframe[] originalKeys = source.keys;
            for (int i = 0; i < originalKeys.Length; i++)
            {
                Keyframe key = originalKeys[i];
                if (!IsFinite(key.time) || !IsFinite(key.value)
                    || !IsFinite(key.inWeight) || !IsFinite(key.outWeight)
                    || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent))
                {
                    error = "Keyframe " + (i + 1) + " contains invalid numeric data.";
                    return false;
                }
            }

            // AnimationCurve.keys is time-sorted; the whole curve supplies the anchor.
            int anchorIndex = anchor == URflowKeyframeMoveAnchor.First ? 0 : originalKeys.Length - 1;
            float originalAnchorValue = originalKeys[anchorIndex].value;
            double amount;
            if (mode == URflowKeyframeMoveMode.Proportional)
            {
                if (originalAnchorValue == 0f)
                {
                    if (targetValue != 0f)
                    {
                        error = "Proportional adjustment cannot use a zero anchor for a nonzero default value.";
                        return false;
                    }
                    // 0 -> 0 is already aligned, even if other keys are nonzero.
                    amount = 1d;
                }
                else
                {
                    amount = (double)targetValue / originalAnchorValue;
                }
            }
            else
            {
                amount = (double)targetValue - originalAnchorValue;
            }

            float displayAmount;
            if (!TryToFloat(amount, out displayAmount))
            {
                error = "The calculated offset or factor exceeds the supported numeric range.";
                return false;
            }
            if (mode == URflowKeyframeMoveMode.Proportional && amount != 0d && displayAmount == 0f)
            {
                error = "The proportional factor is too small to represent reliably.";
                return false;
            }

            Keyframe[] transformedKeys = (Keyframe[])originalKeys.Clone();
            bool hasChanges = false;
            for (int i = 0; i < transformedKeys.Length; i++)
            {
                // Copying the whole struct retains weights, weightedMode and tangent-mode bits.
                Keyframe key = originalKeys[i];
                float newValue;
                double value = mode == URflowKeyframeMoveMode.Proportional
                    ? (double)key.value * amount
                    : TranslateValue(key.value, originalAnchorValue, targetValue);
                if (!TryToFloat(value, out newValue))
                {
                    error = "Moving keyframe " + (i + 1) + " would produce an invalid value.";
                    return false;
                }
                key.value = newValue;

                if (mode == URflowKeyframeMoveMode.Proportional)
                {
                    float newInTangent;
                    float newOutTangent;
                    if (!TryScaleTangent(key.inTangent, amount, out newInTangent)
                        || !TryScaleTangent(key.outTangent, amount, out newOutTangent))
                    {
                        error = "Scaling keyframe " + (i + 1) + " would overflow its tangent slope.";
                        return false;
                    }
                    key.inTangent = newInTangent;
                    key.outTangent = newOutTangent;
                }

                if (key.value != originalKeys[i].value
                    || key.inTangent != originalKeys[i].inTangent
                    || key.outTangent != originalKeys[i].outTangent)
                    hasChanges = true;
                transformedKeys[i] = key;
            }

            AnimationCurve transformedCurve = new AnimationCurve(transformedKeys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
            result = new URflowKeyframeMoveMathResult(
                transformedCurve, originalAnchorValue, targetValue, displayAmount,
                hasChanges, anchor, mode);
            return true;
        }

        private static double TranslateValue(float value, float anchorValue, float targetValue)
        {
            // Sum the same uniform offset in descending magnitude. Opposing large
            // operands cancel before a small target/value is added, including no-ops.
            double first = value;
            double second = -(double)anchorValue;
            double third = targetValue;
            if (Math.Abs(first) < Math.Abs(second)) Swap(ref first, ref second);
            if (Math.Abs(second) < Math.Abs(third)) Swap(ref second, ref third);
            if (Math.Abs(first) < Math.Abs(second)) Swap(ref first, ref second);
            return (first + second) + third;
        }

        private static void Swap(ref double first, ref double second)
        {
            double temporary = first;
            first = second;
            second = temporary;
        }

        private static bool TryScaleTangent(float tangent, double factor, out float result)
        {
            // Infinity is the step-tangent sentinel. Preserve it, including at factor zero.
            if (float.IsInfinity(tangent))
            {
                result = tangent;
                return true;
            }
            return TryToFloat((double)tangent * factor, out result);
        }

        private static bool TryToFloat(double value, out float result)
        {
            result = (float)value;
            return !double.IsNaN(value) && !double.IsInfinity(value) && IsFinite(result);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
