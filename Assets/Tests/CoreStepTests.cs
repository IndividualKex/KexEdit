using KexEdit.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class CoreStepTests {
        private const float TOLERANCE = 1e-5f;

        [Test]
        public void FrameChange_FromAngles_AppliesPitchAndYaw() {
            Frame frame = Frame.Default;
            float deltaPitch = 0.1f;
            float deltaYaw = 0.05f;

            FrameChange.FromAngles(in frame, deltaPitch, deltaYaw, out var change);
            Frame expectedPitched = frame.WithPitch(deltaPitch);
            Frame expected = expectedPitched.WithYaw(deltaYaw);

            Assert.AreEqual(expected.Direction.x, change.NewDirection.x, TOLERANCE);
            Assert.AreEqual(expected.Direction.y, change.NewDirection.y, TOLERANCE);
            Assert.AreEqual(expected.Direction.z, change.NewDirection.z, TOLERANCE);
        }

        [Test]
        public void FrameChange_FromAxis_AppliesRotation() {
            Frame frame = Frame.Default;
            float3 axis = math.up();
            float angle = 0.2f;

            FrameChange.FromAxis(in frame, axis, angle, out var change);
            Frame expected = frame.RotateAround(axis, angle);

            Assert.AreEqual(expected.Direction.x, change.NewDirection.x, TOLERANCE);
            Assert.AreEqual(expected.Direction.y, change.NewDirection.y, TOLERANCE);
            Assert.AreEqual(expected.Direction.z, change.NewDirection.z, TOLERANCE);
        }

        [Test]
        public void Point_ToPoint_PreservesAllFields() {
            var point = new Point(
                spinePosition: new float3(1, 2, 3),
                direction: math.normalize(new float3(0, 0, -1)),
                normal: math.normalize(new float3(0, -1, 0)),
                lateral: math.normalize(new float3(1, 0, 0)),
                velocity: 25f,
                energy: 500f,
                normalForce: 1.5f,
                lateralForce: 0.3f,
                heartArc: 100f,
                spineArc: 95f,
                spineAdvance: 0.25f,
                frictionOrigin: 10f
            );

            Assert.AreEqual(1f, point.SpinePosition.x, TOLERANCE);
            Assert.AreEqual(2f, point.SpinePosition.y, TOLERANCE);
            Assert.AreEqual(3f, point.SpinePosition.z, TOLERANCE);
            Assert.AreEqual(25f, point.Velocity, TOLERANCE);
            Assert.AreEqual(500f, point.Energy, TOLERANCE);
            Assert.AreEqual(1.5f, point.NormalForce, TOLERANCE);
            Assert.AreEqual(0.3f, point.LateralForce, TOLERANCE);
            Assert.AreEqual(100f, point.HeartArc, TOLERANCE);
            Assert.AreEqual(95f, point.SpineArc, TOLERANCE);
        }

        [Test]
        public void Point_Construction_StoresAllFields() {
            var point = new Point(
                spinePosition: new float3(5, 10, -20),
                direction: math.normalize(new float3(0.5f, 0.1f, -1)),
                normal: math.normalize(new float3(0, -1, 0)),
                lateral: math.normalize(new float3(1, 0, 0)),
                velocity: 30f,
                energy: 600f,
                normalForce: 2.0f,
                lateralForce: 0.5f,
                heartArc: 150f,
                spineArc: 145f,
                spineAdvance: 0.3f,
                frictionOrigin: 20f
            );

            Assert.AreEqual(5f, point.SpinePosition.x, TOLERANCE);
            Assert.AreEqual(10f, point.SpinePosition.y, TOLERANCE);
            Assert.AreEqual(-20f, point.SpinePosition.z, TOLERANCE);
            Assert.AreEqual(30f, point.Velocity, TOLERANCE);
            Assert.AreEqual(600f, point.Energy, TOLERANCE);
            Assert.AreEqual(0.3f, point.SpineAdvance, TOLERANCE);
            Assert.AreEqual(20f, point.FrictionOrigin, TOLERANCE);
        }
    }
}
