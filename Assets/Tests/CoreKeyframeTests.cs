using KexEdit.Sim;
using NUnit.Framework;
using Unity.Collections;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class CoreKeyframeTests {
        private const float TOLERANCE = 1e-4f;

        private NativeArray<Keyframe> CreateKeyframes(params Keyframe[] keyframes) {
            var array = new NativeArray<Keyframe>(keyframes.Length, Allocator.Temp);
            for (int i = 0; i < keyframes.Length; i++) {
                array[i] = keyframes[i];
            }
            return array;
        }

        [Test]
        public void Evaluate_EmptyArray_ReturnsDefault() {
            var keyframes = new NativeArray<Keyframe>(0, Allocator.Temp);
            float result = KeyframeEvaluator.Evaluate(keyframes, 0.5f, 42f);
            keyframes.Dispose();

            Assert.AreEqual(42f, result, TOLERANCE, "Should return default value for empty array");
        }

        [Test]
        public void Evaluate_UninitializedArray_ReturnsDefault() {
            NativeArray<Keyframe> keyframes = default;
            float result = KeyframeEvaluator.Evaluate(keyframes, 0.5f, 99f);

            Assert.AreEqual(99f, result, TOLERANCE, "Should return default value for uninitialized array");
        }

        [Test]
        public void Evaluate_SingleKeyframe_ReturnsValue() {
            var keyframes = CreateKeyframes(new Keyframe(1f, 5f));
            float result = KeyframeEvaluator.Evaluate(keyframes, 0.5f);
            keyframes.Dispose();

            Assert.AreEqual(5f, result, TOLERANCE);
        }

        [Test]
        public void Evaluate_BeforeFirstKeyframe_ReturnsFirstValue() {
            var keyframes = CreateKeyframes(
                new Keyframe(1f, 10f),
                new Keyframe(2f, 20f)
            );
            float result = KeyframeEvaluator.Evaluate(keyframes, 0f);
            keyframes.Dispose();

            Assert.AreEqual(10f, result, TOLERANCE, "Should return first value when t < first keyframe time");
        }

        [Test]
        public void Evaluate_AfterLastKeyframe_ReturnsLastValue() {
            var keyframes = CreateKeyframes(
                new Keyframe(1f, 10f),
                new Keyframe(2f, 20f)
            );
            float result = KeyframeEvaluator.Evaluate(keyframes, 5f);
            keyframes.Dispose();

            Assert.AreEqual(20f, result, TOLERANCE, "Should return last value when t > last keyframe time");
        }

        [Test]
        public void Evaluate_ExactlyOnKeyframe_ReturnsKeyframeValue() {
            var keyframes = CreateKeyframes(
                new Keyframe(1f, 10f),
                new Keyframe(2f, 20f),
                new Keyframe(3f, 30f)
            );
            float result = KeyframeEvaluator.Evaluate(keyframes, 2f);
            keyframes.Dispose();

            Assert.AreEqual(20f, result, TOLERANCE);
        }

        [Test]
        public void Evaluate_ConstantInterpolation_ReturnsStartValue() {
            var keyframes = CreateKeyframes(
                new Keyframe(0f, 10f, InterpolationType.Constant, InterpolationType.Constant),
                new Keyframe(2f, 20f, InterpolationType.Constant, InterpolationType.Constant)
            );
            float result = KeyframeEvaluator.Evaluate(keyframes, 1f);
            keyframes.Dispose();

            Assert.AreEqual(10f, result, TOLERANCE, "Constant interpolation should hold start value");
        }

        [Test]
        public void Evaluate_LinearInterpolation_InterpolatesCorrectly() {
            var keyframes = CreateKeyframes(
                new Keyframe(0f, 0f, InterpolationType.Linear, InterpolationType.Linear),
                new Keyframe(2f, 10f, InterpolationType.Linear, InterpolationType.Linear)
            );
            float result = KeyframeEvaluator.Evaluate(keyframes, 1f);
            keyframes.Dispose();

            Assert.AreEqual(5f, result, TOLERANCE, "Linear interpolation should give midpoint value");
        }

        [Test]
        public void Evaluate_LinearInterpolation_Midpoint_ReturnsAverage() {
            var keyframes = CreateKeyframes(
                new Keyframe(0f, 100f, InterpolationType.Linear, InterpolationType.Linear),
                new Keyframe(1f, 200f, InterpolationType.Linear, InterpolationType.Linear)
            );
            float result = KeyframeEvaluator.Evaluate(keyframes, 0.5f);
            keyframes.Dispose();

            Assert.AreEqual(150f, result, TOLERANCE);
        }

        [Test]
        public void Evaluate_BezierInterpolation_SmoothCurve() {
            var keyframes = CreateKeyframes(
                new Keyframe(0f, 0f, InterpolationType.Bezier, InterpolationType.Bezier, 0f, 0f, 1f/3f, 1f/3f),
                new Keyframe(1f, 1f, InterpolationType.Bezier, InterpolationType.Bezier, 0f, 0f, 1f/3f, 1f/3f)
            );

            float result = KeyframeEvaluator.Evaluate(keyframes, 0.5f);
            keyframes.Dispose();

            Assert.Greater(result, 0f, "Bezier should produce positive value at midpoint");
            Assert.Less(result, 1f, "Bezier should produce value less than end at midpoint");
        }

        [Test]
        public void Evaluate_BezierInterpolation_WithTangents() {
            var keyframes = CreateKeyframes(
                new Keyframe(0f, 0f, InterpolationType.Bezier, InterpolationType.Bezier, 0f, 2f, 1f/3f, 1f/3f),
                new Keyframe(1f, 1f, InterpolationType.Bezier, InterpolationType.Bezier, 2f, 0f, 1f/3f, 1f/3f)
            );

            float resultAt25 = KeyframeEvaluator.Evaluate(keyframes, 0.25f);
            float resultAt75 = KeyframeEvaluator.Evaluate(keyframes, 0.75f);
            keyframes.Dispose();

            Assert.Greater(resultAt25, 0f, "Bezier should produce positive values");
            Assert.Less(resultAt75, 1f, "Bezier should stay within bounds");
            Assert.Greater(resultAt75, resultAt25, "Values should increase monotonically");
        }

        [Test]
        public void Evaluate_MultipleKeyframes_FindsCorrectSegment() {
            var keyframes = CreateKeyframes(
                new Keyframe(0f, 0f, InterpolationType.Linear, InterpolationType.Linear),
                new Keyframe(1f, 10f, InterpolationType.Linear, InterpolationType.Linear),
                new Keyframe(2f, 20f, InterpolationType.Linear, InterpolationType.Linear),
                new Keyframe(3f, 30f, InterpolationType.Linear, InterpolationType.Linear)
            );

            float result1 = KeyframeEvaluator.Evaluate(keyframes, 0.5f);
            float result2 = KeyframeEvaluator.Evaluate(keyframes, 1.5f);
            float result3 = KeyframeEvaluator.Evaluate(keyframes, 2.5f);
            keyframes.Dispose();

            Assert.AreEqual(5f, result1, TOLERANCE, "First segment");
            Assert.AreEqual(15f, result2, TOLERANCE, "Second segment");
            Assert.AreEqual(25f, result3, TOLERANCE, "Third segment");
        }

        [Test]
        public void EvaluateSegment_ConstantOutInterpolation_ReturnsStartValue() {
            var start = new Keyframe(0f, 100f, InterpolationType.Bezier, InterpolationType.Constant);
            var end = new Keyframe(1f, 200f, InterpolationType.Bezier, InterpolationType.Bezier);

            float result = KeyframeEvaluator.EvaluateSegment(start, end, 0.5f);

            Assert.AreEqual(100f, result, TOLERANCE, "Constant out should hold start value");
        }

        [Test]
        public void EvaluateSegment_ConstantIn_DoesNotAffectSegment() {
            // Constant IN on end keyframe should NOT cause immediate jump
            // Per industry standard (Blender/After Effects), constant is an OUTGOING property
            var start = new Keyframe(0f, 100f, InterpolationType.Bezier, InterpolationType.Bezier);
            var end = new Keyframe(1f, 200f, InterpolationType.Constant, InterpolationType.Bezier);

            float midpoint = KeyframeEvaluator.EvaluateSegment(start, end, 0.5f);

            Assert.Greater(midpoint, 100f, "Should interpolate, not hold at start");
            Assert.Less(midpoint, 200f, "Should interpolate, not jump to end");
        }

        [Test]
        public void EvaluateSegment_ConstantOut_HoldsEntireSegment() {
            var start = new Keyframe(0f, 100f, InterpolationType.Bezier, InterpolationType.Constant);
            var end = new Keyframe(1f, 200f, InterpolationType.Bezier, InterpolationType.Bezier);

            Assert.AreEqual(100f, KeyframeEvaluator.EvaluateSegment(start, end, 0.0f), TOLERANCE);
            Assert.AreEqual(100f, KeyframeEvaluator.EvaluateSegment(start, end, 0.5f), TOLERANCE);
            Assert.AreEqual(100f, KeyframeEvaluator.EvaluateSegment(start, end, 0.99f), TOLERANCE);
        }

        [TestCase(0.1f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        [TestCase(0.9f)]
        public void Evaluate_Linear_MonotonicBehavior(float t) {
            var keyframes = CreateKeyframes(
                new Keyframe(0f, 0f, InterpolationType.Linear, InterpolationType.Linear),
                new Keyframe(1f, 100f, InterpolationType.Linear, InterpolationType.Linear)
            );
            float result = KeyframeEvaluator.Evaluate(keyframes, t);
            keyframes.Dispose();

            float expected = t * 100f;
            Assert.AreEqual(expected, result, TOLERANCE, $"Linear at t={t}");
        }

        [Test]
        public void Evaluate_BezierConvergence_LargeWeights() {
            var keyframes = CreateKeyframes(
                new Keyframe(0f, 0f, InterpolationType.Bezier, InterpolationType.Bezier, 0f, 5f, 0.9f, 0.9f),
                new Keyframe(1f, 1f, InterpolationType.Bezier, InterpolationType.Bezier, 5f, 0f, 0.9f, 0.9f)
            );

            Assert.DoesNotThrow(() => {
                for (float t = 0f; t <= 1f; t += 0.1f) {
                    KeyframeEvaluator.Evaluate(keyframes, t);
                }
            }, "Newton's method should converge even with large weights");

            keyframes.Dispose();
        }
    }
}
