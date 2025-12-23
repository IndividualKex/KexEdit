using NUnit.Framework;
using Unity.Mathematics;
using KexEdit.Native.RustCore;
using CoreFrame = KexEdit.Sim.Frame;

namespace KexEdit.Tests.RustCore {
    public class RustFrameTests {
        private const float EPSILON = 1e-6f;

        [Test]
        public void TestRustFrameConversion() {
            CoreFrame csharpFrame = CoreFrame.Default;
            RustFrame rustFrame = RustFrame.FromCore(csharpFrame);
            CoreFrame convertedBack = rustFrame.ToCore();

            AssertFrameEquals(csharpFrame, convertedBack);
        }

        [Test]
        public void TestRotateAroundMatchesCSharp() {
            CoreFrame csharpFrame = CoreFrame.Default;
            float3 axis = math.up();
            float angle = math.PI / 2f;

            CoreFrame csharpResult = csharpFrame.RotateAround(axis, angle);

            RustFrame rustFrame = RustFrame.FromCore(csharpFrame);
            RustFloat3 rustAxis = RustFloat3.FromUnity(axis);
            RustFrame rustResult = RustFrameNative.RotateAround(rustFrame, rustAxis, angle);
            CoreFrame rustResultCore = rustResult.ToCore();

            AssertFrameEquals(csharpResult, rustResultCore);
        }

        [Test]
        public void TestWithRollMatchesCSharp() {
            CoreFrame csharpFrame = CoreFrame.Default;
            float deltaRoll = math.PI / 4f;

            CoreFrame csharpResult = csharpFrame.WithRoll(deltaRoll);

            RustFrame rustFrame = RustFrame.FromCore(csharpFrame);
            RustFrame rustResult = RustFrameNative.WithRoll(rustFrame, deltaRoll);
            CoreFrame rustResultCore = rustResult.ToCore();

            AssertFrameEquals(csharpResult, rustResultCore);
        }

        [Test]
        public void TestWithPitchMatchesCSharp() {
            CoreFrame csharpFrame = CoreFrame.Default;
            float deltaPitch = math.PI / 6f;

            CoreFrame csharpResult = csharpFrame.WithPitch(deltaPitch);

            RustFrame rustFrame = RustFrame.FromCore(csharpFrame);
            RustFrame rustResult = RustFrameNative.WithPitch(rustFrame, deltaPitch);
            CoreFrame rustResultCore = rustResult.ToCore();

            AssertFrameEquals(csharpResult, rustResultCore);
        }

        [Test]
        public void TestWithYawMatchesCSharp() {
            CoreFrame csharpFrame = CoreFrame.Default;
            float deltaYaw = math.PI / 3f;

            CoreFrame csharpResult = csharpFrame.WithYaw(deltaYaw);

            RustFrame rustFrame = RustFrame.FromCore(csharpFrame);
            RustFrame rustResult = RustFrameNative.WithYaw(rustFrame, deltaYaw);
            CoreFrame rustResultCore = rustResult.ToCore();

            AssertFrameEquals(csharpResult, rustResultCore);
        }

        [Test]
        public void TestRollMatchesCSharp() {
            CoreFrame csharpFrame = CoreFrame.Default.WithRoll(math.PI / 4f);

            float csharpRoll = csharpFrame.Roll;

            RustFrame rustFrame = RustFrame.FromCore(csharpFrame);
            float rustRoll = RustFrameNative.Roll(rustFrame);

            Assert.AreEqual(csharpRoll, rustRoll, EPSILON);
        }

        private void AssertFrameEquals(CoreFrame expected, CoreFrame actual) {
            AssertFloat3Equals(expected.Direction, actual.Direction, "Direction");
            AssertFloat3Equals(expected.Normal, actual.Normal, "Normal");
            AssertFloat3Equals(expected.Lateral, actual.Lateral, "Lateral");
        }

        private void AssertFloat3Equals(float3 expected, float3 actual, string name) {
            Assert.AreEqual(expected.x, actual.x, EPSILON, $"{name}.x mismatch");
            Assert.AreEqual(expected.y, actual.y, EPSILON, $"{name}.y mismatch");
            Assert.AreEqual(expected.z, actual.z, EPSILON, $"{name}.z mismatch");
        }
    }
}
