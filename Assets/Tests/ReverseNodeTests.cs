using KexEdit.Core;
using KexEdit.Nodes.Reverse;
using NUnit.Framework;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    public class ReverseNodeTests {
        [Test]
        public void Build_ReversesDirection() {
            Point anchor = CreateTestPoint();

            ReverseNode.Build(in anchor, out Point result);

            Assert.AreEqual(-anchor.Direction.x, result.Direction.x, 1e-5f);
            Assert.AreEqual(-anchor.Direction.y, result.Direction.y, 1e-5f);
            Assert.AreEqual(-anchor.Direction.z, result.Direction.z, 1e-5f);
        }

        [Test]
        public void Build_ReversesLateral() {
            Point anchor = CreateTestPoint();

            ReverseNode.Build(in anchor, out Point result);

            Assert.AreEqual(-anchor.Lateral.x, result.Lateral.x, 1e-5f);
            Assert.AreEqual(-anchor.Lateral.y, result.Lateral.y, 1e-5f);
            Assert.AreEqual(-anchor.Lateral.z, result.Lateral.z, 1e-5f);
        }

        [Test]
        public void Build_PreservesNormal() {
            Point anchor = CreateTestPoint();

            ReverseNode.Build(in anchor, out Point result);

            Assert.AreEqual(anchor.Normal.x, result.Normal.x, 1e-5f);
            Assert.AreEqual(anchor.Normal.y, result.Normal.y, 1e-5f);
            Assert.AreEqual(anchor.Normal.z, result.Normal.z, 1e-5f);
        }

        [Test]
        public void Build_PreservesPosition() {
            Point anchor = CreateTestPoint();

            ReverseNode.Build(in anchor, out Point result);

            Assert.AreEqual(anchor.SpinePosition.x, result.SpinePosition.x, 1e-5f);
            Assert.AreEqual(anchor.SpinePosition.y, result.SpinePosition.y, 1e-5f);
            Assert.AreEqual(anchor.SpinePosition.z, result.SpinePosition.z, 1e-5f);
        }

        [Test]
        public void Build_PreservesVelocityAndEnergy() {
            Point anchor = CreateTestPoint();

            ReverseNode.Build(in anchor, out Point result);

            Assert.AreEqual(anchor.Velocity, result.Velocity, 1e-5f);
            Assert.AreEqual(anchor.Energy, result.Energy, 1e-5f);
        }

        [Test]
        public void Build_ReversesLateralForce() {
            Point anchor = CreateTestPoint();

            ReverseNode.Build(in anchor, out Point result);

            Assert.AreEqual(-anchor.LateralForce, result.LateralForce, 1e-5f);
        }

        private static Point CreateTestPoint() {
            return new Point(
                spinePosition: new float3(1f, 2f, 3f),
                direction: new float3(0f, 0f, 1f),
                normal: new float3(0f, 1f, 0f),
                lateral: new float3(1f, 0f, 0f),
                velocity: 10f,
                energy: 100f,
                normalForce: 1.5f,
                lateralForce: 0.5f,
                heartArc: 50f,
                spineArc: 55f,
                spineAdvance: 0.1f,
                frictionOrigin: 10f
            );
        }
    }
}
