using System;
using UnityEditor;
using UnityEngine;

namespace URflow.Tests
{
    // Copy into the dedicated test project's Assets/Editor and call Run from its harness.
    public static class URflowKeyframeMoveMathChecks
    {
        private static int _passed;

        public static int Run()
        {
            _passed = 0;
            Check("whole weighted curve translation", Translation);
            Check("earliest anchor from time-sorted keys", FirstAnchor);
            Check("single key alignment", SingleKey);
            Check("weighted proportional scaling", PositiveFactor);
            Check("tangent modes and Broken state", TangentMetadata);
            Check("constant curve translation and ratio", ConstantCurve);
            Check("zero factor keeps step tangents", ZeroFactor);
            Check("negative factor and tangent signs", NegativeFactor);
            Check("signed infinite tangents stay unchanged", SignedInfinity);
            Check("zero anchor with nonzero target rejected", ZeroAnchorRejected);
            Check("zero anchor and target preserve other keys", ZeroAnchorNoOp);
            Check("already aligned curve is an independent clone", AlreadyAligned);
            Check("non-finite target rejected", InvalidTarget);
            Check("empty input and unsupported options rejected", InvalidInput);
            Check("translation overflow rejected", TranslationOverflow);
            Check("ratio value and tangent overflow rejected", ProportionalOverflow);
            Check("unrepresentable factor rejected", FactorRange);
            Check("small real change is not discarded", SmallChange);
            Check("translation preserves small results after large cancellation", TranslationCancellation);
            Debug.Log("URFlow keyframe movement math: " + _passed + " checks passed.");
            return _passed;
        }

        private static void Check(string name, Action action)
        {
            try
            {
                action();
                _passed++;
                Debug.Log("PASS: " + name);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Math check failed: " + name, ex);
            }
        }

        private static void Translation()
        {
            AnimationCurve source = WeightedCurve(-200f, -80f, 0f);
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult result = Build(source, 50f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Translate);
            Equal(-150f, result.Curve.keys[0].value, "first value");
            Equal(-30f, result.Curve.keys[1].value, "middle value");
            Equal(50f, result.Curve.keys[2].value, "last value");
            Require(result.HasChanges && result.DeltaOrFactor == 50f, "translation result");
            Require(!result.IsZeroFactor && !result.IsNegativeFactor, "translation flags");
            Metadata(source, result.Curve, 1f, true);
            SampledTransform(source, result.Curve, 1f, 50f);
            snapshot.Unchanged(source);
            Require(!ReferenceEquals(source, result.Curve), "source curve was reused");
        }

        private static void FirstAnchor()
        {
            AnimationCurve source = new AnimationCurve(
                new Keyframe(2f, 30f, 1f, 1f),
                new Keyframe(0f, 10f, 2f, 2f),
                new Keyframe(1f, 20f, 3f, 3f));
            URflowKeyframeMoveMathResult result = Build(source, 5f,
                URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate);
            Require(result.OriginalAnchorValue == 10f, "earliest anchor was not used");
            Equal(5f, result.Curve.keys[0].value, "earliest value");
            Equal(15f, result.Curve.keys[1].value, "middle value");
            Equal(25f, result.Curve.keys[2].value, "latest value");
            Metadata(source, result.Curve, 1f, true);
        }

        private static void SingleKey()
        {
            AnimationCurve source = new AnimationCurve(new Keyframe(3f, -3f, 2f, -4f, 0.2f, 0.7f));
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult result = Build(source, 6f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            Equal(6f, result.Curve.keys[0].value, "single key");
            Require(result.IsNegativeFactor, "single negative factor");
            Metadata(source, result.Curve, -2f, false);
            snapshot.Unchanged(source);
        }

        private static void PositiveFactor()
        {
            AnimationCurve source = WeightedCurve(0f, 0.6f, 1f);
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult result = Build(source, 1.5f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            Equal(0f, result.Curve.keys[0].value, "scaled first");
            Equal(0.9f, result.Curve.keys[1].value, "scaled middle");
            Equal(1.5f, result.Curve.keys[2].value, "scaled last");
            Require(result.DeltaOrFactor == 1.5f && result.HasChanges, "positive factor");
            Metadata(source, result.Curve, 1.5f, false);
            SampledTransform(source, result.Curve, 1.5f, 0f);
            snapshot.Unchanged(source);
        }

        private static void TangentMetadata()
        {
            AnimationCurve source = new AnimationCurve(
                new Keyframe(0f, 2f), new Keyframe(0.25f, 4f),
                new Keyframe(0.65f, 3f), new Keyframe(1f, 6f));
            AnimationUtility.SetKeyBroken(source, 0, true);
            AnimationUtility.SetKeyLeftTangentMode(source, 0, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(source, 0, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyBroken(source, 1, false);
            AnimationUtility.SetKeyLeftTangentMode(source, 1, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(source, 1, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyBroken(source, 2, false);
            AnimationUtility.SetKeyLeftTangentMode(source, 2, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(source, 2, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyBroken(source, 3, true);
            AnimationUtility.SetKeyLeftTangentMode(source, 3, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(source, 3, AnimationUtility.TangentMode.Free);
            source.preWrapMode = WrapMode.Loop;
            source.postWrapMode = WrapMode.PingPong;
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult translated = Build(source, 8f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Translate);
            Metadata(source, translated.Curve, 1f, true);
            URflowKeyframeMoveMathResult scaled = Build(source, -12f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            Metadata(source, scaled.Curve, -2f, false);
            snapshot.Unchanged(source);
        }

        private static void ConstantCurve()
        {
            AnimationCurve source = new AnimationCurve(
                new Keyframe(0f, 4f, 0f, 0f),
                new Keyframe(0.4f, 4f, 0f, 0f),
                new Keyframe(1f, 4f, 0f, 0f));
            URflowKeyframeMoveMathResult translated = Build(source, 7f,
                URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate);
            URflowKeyframeMoveMathResult scaled = Build(source, 2f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            for (int i = 0; i < source.length; i++)
            {
                Equal(7f, translated.Curve.keys[i].value, "constant translated");
                Equal(2f, scaled.Curve.keys[i].value, "constant scaled");
            }
            for (int i = 0; i <= 10; i++)
            {
                Equal(7f, translated.Curve.Evaluate(i / 10f), "constant translated sample");
                Equal(2f, scaled.Curve.Evaluate(i / 10f), "constant scaled sample");
            }
            Metadata(source, translated.Curve, 1f, true);
            Metadata(source, scaled.Curve, 0.5f, false);
        }

        private static void ZeroFactor()
        {
            AnimationCurve source = new AnimationCurve(new Keyframe(0f, 2f), new Keyframe(1f, 4f));
            for (int i = 0; i < source.length; i++)
            {
                AnimationUtility.SetKeyBroken(source, i, true);
                AnimationUtility.SetKeyLeftTangentMode(source, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(source, i, AnimationUtility.TangentMode.Constant);
            }
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult result = Build(source, 0f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            Require(result.IsZeroFactor && result.HasChanges, "zero factor flags");
            Require(!string.IsNullOrEmpty(result.Warning), "zero factor warning");
            Metadata(source, result.Curve, 0f, false);
            for (int i = 0; i <= 10; i++)
                Equal(0f, result.Curve.Evaluate(i / 10f), "zero step sample");
            snapshot.Unchanged(source);
        }

        private static void NegativeFactor()
        {
            AnimationCurve source = WeightedCurve(-100f, 20f, 100f);
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult result = Build(source, -200f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            Equal(200f, result.Curve.keys[0].value, "negative first");
            Equal(-40f, result.Curve.keys[1].value, "negative middle");
            Equal(-200f, result.Curve.keys[2].value, "negative last");
            Require(result.IsNegativeFactor && result.HasChanges, "negative factor flags");
            Require(!string.IsNullOrEmpty(result.Warning), "negative factor warning");
            Metadata(source, result.Curve, -2f, false);
            SampledTransform(source, result.Curve, -2f, 0f);
            snapshot.Unchanged(source);
        }

        private static void SignedInfinity()
        {
            AnimationCurve source = new AnimationCurve(
                new Keyframe(0f, 1f, float.NegativeInfinity, float.PositiveInfinity),
                new Keyframe(1f, 3f, float.PositiveInfinity, float.NegativeInfinity));
            URflowKeyframeMoveMathResult result = Build(source, -6f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            Metadata(source, result.Curve, -2f, false);
        }

        private static void ZeroAnchorRejected()
        {
            AnimationCurve source = WeightedCurve(0f, 1f, 2f);
            Reject(source, 1f, URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Proportional);
        }

        private static void ZeroAnchorNoOp()
        {
            AnimationCurve source = WeightedCurve(0f, 1f, 2f);
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult result = Build(source, 0f,
                URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Proportional);
            Require(!result.HasChanges && result.DeltaOrFactor == 1f, "0 to 0 must not collapse other keys");
            Require(!result.IsZeroFactor && !result.IsNegativeFactor, "no-op flags");
            Require(result.Curve.keys[1].Equals(source.keys[1]), "other key changed");
            Metadata(source, result.Curve, 1f, true);
            snapshot.Unchanged(source);
        }

        private static void AlreadyAligned()
        {
            AnimationCurve source = WeightedCurve(2f, 3f, 4f);
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult translated = Build(source, 4f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Translate);
            URflowKeyframeMoveMathResult scaled = Build(source, 4f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            Require(!translated.HasChanges && !scaled.HasChanges, "aligned curve has changes");
            Require(translated.DeltaOrFactor == 0f && scaled.DeltaOrFactor == 1f, "aligned amount");
            Require(!ReferenceEquals(source, translated.Curve), "aligned curve not cloned");
            Keyframe altered = translated.Curve.keys[0];
            altered.value = 99f;
            translated.Curve.MoveKey(0, altered);
            snapshot.Unchanged(source);
        }

        private static void InvalidTarget()
        {
            AnimationCurve source = WeightedCurve(1f, 2f, 3f);
            Reject(source, float.NaN, URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate);
            Reject(source, float.PositiveInfinity, URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            Reject(source, float.NegativeInfinity, URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Translate);
        }

        private static void InvalidInput()
        {
            Reject(null, 1f, URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate);
            Reject(new AnimationCurve(), 1f, URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            AnimationCurve source = WeightedCurve(1f, 2f, 3f);
            Reject(source, 1f, (URflowKeyframeMoveAnchor)99, URflowKeyframeMoveMode.Translate);
            Reject(source, 1f, URflowKeyframeMoveAnchor.First, (URflowKeyframeMoveMode)99);
        }

        private static void TranslationOverflow()
        {
            AnimationCurve source = new AnimationCurve(
                new Keyframe(0f, float.MaxValue), new Keyframe(1f, -float.MaxValue));
            Reject(source, -float.MaxValue, URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate);
        }

        private static void ProportionalOverflow()
        {
            AnimationCurve source = new AnimationCurve(
                new Keyframe(0f, float.MaxValue / 2f), new Keyframe(1f, 1f));
            Reject(source, 4f, URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
            source = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, float.MaxValue), new Keyframe(1f, 2f, 0f, 0f));
            Reject(source, 4f, URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional);
        }

        private static void FactorRange()
        {
            AnimationCurve source = new AnimationCurve(
                new Keyframe(0f, float.Epsilon), new Keyframe(1f, 1f));
            Reject(source, 1f, URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Proportional);
            source = new AnimationCurve(new Keyframe(0f, float.MaxValue));
            Reject(source, float.Epsilon, URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Proportional);
        }

        private static void SmallChange()
        {
            AnimationCurve source = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 2f));
            float target = 1.00000011920928955078125f;
            URflowKeyframeMoveMathResult result = Build(source, target,
                URflowKeyframeMoveAnchor.First, URflowKeyframeMoveMode.Translate);
            Require(result.HasChanges && result.Curve.keys[0].value == target, "small shift was discarded");
        }

        private static void TranslationCancellation()
        {
            AnimationCurve source = new AnimationCurve(
                new Keyframe(0f, 1e20f, 2f, -3f), new Keyframe(1f, 1e20f, 4f, -5f));
            SourceSnapshot snapshot = new SourceSnapshot(source);
            URflowKeyframeMoveMathResult result = Build(source, 1f,
                URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Translate);
            Require(result.Curve.keys[0].value == 1f && result.Curve.keys[1].value == 1f,
                "large anchor canceled the small target");
            Metadata(source, result.Curve, 1f, true);
            snapshot.Unchanged(source);

            source = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1e20f));
            snapshot = new SourceSnapshot(source);
            result = Build(source, 1e20f, URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Translate);
            Require(!result.HasChanges && result.Curve.keys[0].value == 1f,
                "large equal anchor/target changed a small unrelated key");
            snapshot.Unchanged(source);

            source = new AnimationCurve(new Keyframe(0f, -1e20f), new Keyframe(1f, 1f));
            snapshot = new SourceSnapshot(source);
            result = Build(source, 1e20f, URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Translate);
            Require(result.Curve.keys[0].value == -1f && result.Curve.keys[1].value == 1e20f,
                "large key/target cancellation lost the small anchor offset");
            snapshot.Unchanged(source);
        }

        private static AnimationCurve WeightedCurve(params float[] values)
        {
            Keyframe[] keys = new Keyframe[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float time = values.Length == 1 ? 0f : i / (float)(values.Length - 1);
                keys[i] = new Keyframe(time, values[i], 1.25f + i * 0.5f, -0.75f + i * 0.4f,
                    0.15f + i * 0.1f, 0.75f - i * 0.1f);
            }
            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyBroken(curve, i, i % 2 == 0);
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            }
            Keyframe[] restored = curve.keys;
            for (int i = 0; i < restored.Length; i++)
            {
                restored[i].inTangent = keys[i].inTangent;
                restored[i].outTangent = keys[i].outTangent;
                restored[i].inWeight = keys[i].inWeight;
                restored[i].outWeight = keys[i].outWeight;
                restored[i].weightedMode = i % 3 == 0 ? WeightedMode.Both
                    : i % 3 == 1 ? WeightedMode.In : WeightedMode.Out;
            }
            curve.keys = restored;
            curve.preWrapMode = WrapMode.Loop;
            curve.postWrapMode = WrapMode.PingPong;
            return curve;
        }

        private static URflowKeyframeMoveMathResult Build(AnimationCurve source, float target,
            URflowKeyframeMoveAnchor anchor, URflowKeyframeMoveMode mode)
        {
            URflowKeyframeMoveMathResult result;
            string error;
            Require(URflowKeyframeMoveMath.TryBuildCurve(source, target, anchor, mode, out result, out error), error);
            Require(result != null && result.Curve != null, "missing result curve");
            Require(string.IsNullOrEmpty(error), "success returned an error");
            return result;
        }

        private static void Reject(AnimationCurve source, float target,
            URflowKeyframeMoveAnchor anchor, URflowKeyframeMoveMode mode)
        {
            SourceSnapshot snapshot = source == null ? null : new SourceSnapshot(source);
            URflowKeyframeMoveMathResult result;
            string error;
            Require(!URflowKeyframeMoveMath.TryBuildCurve(source, target, anchor, mode, out result, out error), "invalid input accepted");
            Require(result == null && !string.IsNullOrEmpty(error), "invalid input lacks error");
            if (snapshot != null) snapshot.Unchanged(source);
        }

        private static void Metadata(AnimationCurve source, AnimationCurve actual, float factor, bool exactTangents)
        {
            Keyframe[] before = source.keys;
            Keyframe[] after = actual.keys;
            Require(before.Length == after.Length, "key count");
            Require(source.preWrapMode == actual.preWrapMode && source.postWrapMode == actual.postWrapMode, "wrap modes");
            for (int i = 0; i < before.Length; i++)
            {
                Require(before[i].time == after[i].time, "time " + i);
                Require(before[i].inWeight == after[i].inWeight && before[i].outWeight == after[i].outWeight, "weights " + i);
                Require(before[i].weightedMode == after[i].weightedMode, "weighted mode " + i);
                Require(AnimationUtility.GetKeyBroken(source, i) == AnimationUtility.GetKeyBroken(actual, i), "Broken " + i);
                Require(AnimationUtility.GetKeyLeftTangentMode(source, i) == AnimationUtility.GetKeyLeftTangentMode(actual, i), "left mode " + i);
                Require(AnimationUtility.GetKeyRightTangentMode(source, i) == AnimationUtility.GetKeyRightTangentMode(actual, i), "right mode " + i);
                Tangent(before[i].inTangent, after[i].inTangent, factor, exactTangents, "in slope " + i);
                Tangent(before[i].outTangent, after[i].outTangent, factor, exactTangents, "out slope " + i);
            }
        }

        private static void Tangent(float before, float after, float factor, bool exact, string label)
        {
            if (float.IsInfinity(before) || exact)
                Require(before.Equals(after), label);
            else
                Equal(before * factor, after, label);
        }

        private static void SampledTransform(AnimationCurve before, AnimationCurve after, float factor, float delta)
        {
            for (int i = 0; i <= 30; i++)
            {
                float time = -0.15f + i * 0.045f;
                float expected = before.Evaluate(time) * factor + delta;
                float actual = after.Evaluate(time);
                float tolerance = Mathf.Max(0.0002f, Mathf.Abs(expected) * 0.00002f);
                Require(!float.IsNaN(actual) && !float.IsInfinity(actual)
                    && Mathf.Abs(expected - actual) <= tolerance, "sample at " + time + ": " + actual + " expected " + expected);
            }
        }

        private static void Equal(float expected, float actual, string label)
        {
            Require(Mathf.Abs(expected - actual) <= Mathf.Max(0.000001f, Mathf.Abs(expected) * 0.000001f), label + ": " + actual + " expected " + expected);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message ?? "Assertion failed.");
        }

        private sealed class SourceSnapshot
        {
            private readonly Keyframe[] _keys;
            private readonly WrapMode _preWrap;
            private readonly WrapMode _postWrap;

            public SourceSnapshot(AnimationCurve source)
            {
                _keys = source.keys;
                _preWrap = source.preWrapMode;
                _postWrap = source.postWrapMode;
            }

            public void Unchanged(AnimationCurve source)
            {
                Keyframe[] actual = source.keys;
                Require(actual.Length == _keys.Length, "source key count changed");
                Require(source.preWrapMode == _preWrap && source.postWrapMode == _postWrap, "source wrap changed");
                for (int i = 0; i < _keys.Length; i++)
                    Require(actual[i].Equals(_keys[i]), "source key changed: " + i);
            }
        }
    }
}
