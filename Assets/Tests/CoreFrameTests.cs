using KexEdit.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class CoreFrameTests {
        private const float TOLERANCE = 1e-5f;
        private const float ORTHO_TOLERANCE = 1e-6f;

        private void AssertOrthonormal(Frame frame, string context = "") {
            float dirLen = math.length(frame.Direction);
            float normLen = math.length(frame.Normal);
            float latLen = math.length(frame.Lateral);
            float dirDotNorm = math.dot(frame.Direction, frame.Normal);
            float dirDotLat = math.dot(frame.Direction, frame.Lateral);
            float normDotLat = math.dot(frame.Normal, frame.Lateral);

            Assert.AreEqual(1f, dirLen, ORTHO_TOLERANCE, $"{context}Direction not unit length");
            Assert.AreEqual(1f, normLen, ORTHO_TOLERANCE, $"{context}Normal not unit length");
            Assert.AreEqual(1f, latLen, ORTHO_TOLERANCE, $"{context}Lateral not unit length");
            Assert.AreEqual(0f, dirDotNorm, ORTHO_TOLERANCE, $"{context}Direction not perpendicular to Normal");
            Assert.AreEqual(0f, dirDotLat, ORTHO_TOLERANCE, $"{context}Direction not perpendicular to Lateral");
            Assert.AreEqual(0f, normDotLat, ORTHO_TOLERANCE, $"{context}Normal not perpendicular to Lateral");
        }

        [Test]
        public void Default_IsOrthonormal() {
            Frame frame = Frame.Default;
            AssertOrthonormal(frame);
            Assert.AreEqual(math.back().x, frame.Direction.x, TOLERANCE, "Default direction should be back");
            Assert.AreEqual(math.back().y, frame.Direction.y, TOLERANCE);
            Assert.AreEqual(math.back().z, frame.Direction.z, TOLERANCE);
        }

        [Test]
        public void FromDirectionAndRoll_ProducesOrthonormalFrame() {
            float3 direction = math.normalize(new float3(1, 0.5f, -1));
            Frame frame = Frame.FromDirectionAndRoll(direction, 0.3f);
            AssertOrthonormal(frame);
        }

        [Test]
        public void FromDirectionAndRoll_WithZeroRoll_NormalPointsDown() {
            float3 direction = math.normalize(new float3(0, 0, -1));
            Frame frame = Frame.FromDirectionAndRoll(direction, 0f);
            AssertOrthonormal(frame);
            Assert.Less(frame.Normal.y, 0f, "Normal should point downward with zero roll");
        }

        [Test]
        public void FromDirectionAndRoll_WithNonZeroRoll_AppliesRoll() {
            float3 direction = math.back();
            float targetRoll = 0.5f;
            Frame frame = Frame.FromDirectionAndRoll(direction, targetRoll);
            AssertOrthonormal(frame);
            Assert.AreEqual(targetRoll, frame.Roll, TOLERANCE, "Roll should match input");
        }

        [Test]
        public void FromEuler_MatchesFromDirectionAndRoll() {
            float pitch = 0.2f;
            float yaw = 0.3f;
            float roll = 0.4f;

            Frame frame = Frame.FromEuler(pitch, yaw, roll);
            AssertOrthonormal(frame);
            Assert.AreEqual(roll, frame.Roll, TOLERANCE, "Roll should match");
        }

        [TestCase(0f, 0f, -1f, 0f)]
        [TestCase(0f, 0f, -1f, 0.5f)]
        [TestCase(1f, 0f, 0f, 0.3f)]
        [TestCase(0.707f, 0.707f, 0f, -0.2f)]
        public void FromDirectionAndRoll_VariousInputs_ProducesOrthonormalFrame(float dx, float dy, float dz, float roll) {
            float3 direction = math.normalize(new float3(dx, dy, dz));
            Frame frame = Frame.FromDirectionAndRoll(direction, roll);
            AssertOrthonormal(frame, $"dir=({dx},{dy},{dz}) roll={roll}: ");
        }

        [Test]
        public void WithRoll_PreservesDirection() {
            Frame original = Frame.Default;
            float deltaRoll = 0.3f;
            Frame rolled = original.WithRoll(deltaRoll);

            Assert.AreEqual(original.Direction.x, rolled.Direction.x, TOLERANCE, "Direction.x should be preserved");
            Assert.AreEqual(original.Direction.y, rolled.Direction.y, TOLERANCE, "Direction.y should be preserved");
            Assert.AreEqual(original.Direction.z, rolled.Direction.z, TOLERANCE, "Direction.z should be preserved");
        }

        [Test]
        public void WithRoll_PreservesOrthonormality() {
            Frame original = Frame.FromDirectionAndRoll(math.normalize(new float3(1, 0.5f, -1)), 0.2f);
            Frame rolled = original.WithRoll(0.7f);
            AssertOrthonormal(rolled);
        }

        [Test]
        public void WithRoll_AppliesCorrectRollDelta() {
            Frame original = Frame.Default;
            float deltaRoll = 0.3f;
            Frame rolled = original.WithRoll(deltaRoll);

            float expectedRoll = Sim.WrapAngle(original.Roll + deltaRoll);
            Assert.AreEqual(expectedRoll, rolled.Roll, TOLERANCE, "Roll should increase by delta");
        }

        [Test]
        public void WithPitch_RotatesDirectionVertically() {
            Frame original = Frame.Default;
            float deltaPitch = 0.2f;
            Frame pitched = original.WithPitch(deltaPitch);

            AssertOrthonormal(pitched);
            Assert.Greater(pitched.Direction.y, original.Direction.y, "Positive pitch should raise direction");
        }

        [Test]
        public void WithYaw_RotatesDirectionHorizontally() {
            Frame original = Frame.Default;
            float deltaYaw = 0.2f;
            Frame yawed = original.WithYaw(deltaYaw);

            AssertOrthonormal(yawed);
            Assert.Greater(math.abs(yawed.Direction.x - original.Direction.x), TOLERANCE, "Yaw should change Direction.x");
        }

        [Test]
        public void RotateAround_ArbitraryAxis_PreservesOrthonormality() {
            Frame original = Frame.FromDirectionAndRoll(math.normalize(new float3(1, 0.3f, -1)), 0.4f);
            float3 axis = math.normalize(new float3(1, 1, 1));
            Frame rotated = original.RotateAround(axis, 0.5f);
            AssertOrthonormal(rotated);
        }

        [Test]
        public void RotateAround_Up_MatchesYaw() {
            Frame original = Frame.Default;
            float angle = 0.3f;
            Frame rotated = original.RotateAround(math.up(), angle);
            Frame yawed = original.WithYaw(angle);

            Assert.AreEqual(yawed.Direction.x, rotated.Direction.x, TOLERANCE);
            Assert.AreEqual(yawed.Direction.y, rotated.Direction.y, TOLERANCE);
            Assert.AreEqual(yawed.Direction.z, rotated.Direction.z, TOLERANCE);
        }

        [Test]
        public void Roll_ExtractsCorrectAngle() {
            float targetRoll = 0.6f;
            Frame frame = Frame.FromDirectionAndRoll(math.back(), targetRoll);
            Assert.AreEqual(targetRoll, frame.Roll, TOLERANCE);
        }

        [Test]
        public void Pitch_ExtractsCorrectAngle() {
            float targetPitch = 0.3f;
            Frame frame = Frame.FromEuler(targetPitch, 0f, 0f);
            Assert.AreEqual(targetPitch, frame.Pitch, TOLERANCE);
        }

        [Test]
        public void Yaw_ExtractsCorrectAngle() {
            float targetYaw = 0.4f;
            Frame frame = Frame.FromEuler(0f, targetYaw, 0f);
            Assert.AreEqual(targetYaw, frame.Yaw, TOLERANCE);
        }

        [Test]
        public void SpinePosition_OffsetsAlongNormal() {
            Frame frame = Frame.Default;
            float3 heartPos = new float3(10, 5, -20);
            float offset = 1.1f;

            float3 spinePos = frame.SpinePosition(heartPos, offset);
            float3 expected = heartPos + frame.Normal * offset;

            Assert.AreEqual(expected.x, spinePos.x, TOLERANCE);
            Assert.AreEqual(expected.y, spinePos.y, TOLERANCE);
            Assert.AreEqual(expected.z, spinePos.z, TOLERANCE);
        }

        [Test]
        public void ChainedRotations_PreserveOrthonormality() {
            Frame frame = Frame.Default;
            for (int i = 0; i < 100; i++) {
                frame = frame.WithRoll(0.01f);
                frame = frame.WithPitch(0.005f);
                frame = frame.WithYaw(0.003f);
            }
            AssertOrthonormal(frame, "After 100 chained rotations: ");
        }
    }
}
