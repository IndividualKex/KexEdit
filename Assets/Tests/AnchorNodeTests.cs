using KexEdit.Core;
using KexEdit.Nodes.Anchor;
using NUnit.Framework;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    public class AnchorNodeTests {
        [Test]
        public void Build_DefaultPosition_ReturnsValidPoint() {
            float3 position = new(0f, 3f, 0f);
            float velocity = 10f;
            float energy = 100f;

            AnchorNode.Build(in position, 0f, 0f, 0f, velocity, energy, 1.1f, 0f, 0f, out Point result);

            Assert.AreEqual(position.x, result.HeartPosition.x, 1e-5f);
            Assert.AreEqual(position.y, result.HeartPosition.y, 1e-5f);
            Assert.AreEqual(position.z, result.HeartPosition.z, 1e-5f);
            Assert.AreEqual(velocity, result.Velocity, 1e-5f);
            Assert.AreEqual(energy, result.Energy, 1e-5f);
        }

        [Test]
        public void Build_WithRotation_ProducesOrthonormalFrame() {
            float3 position = float3.zero;
            float velocity = 5f;
            float energy = 50f;

            AnchorNode.Build(in position, 0.1f, 0.2f, 0.3f, velocity, energy, 1.1f, 0f, 0f, out Point result);

            float3 d = result.Direction;
            float3 l = result.Lateral;
            float3 n = result.Normal;

            float dotDL = math.dot(d, l);
            float dotDN = math.dot(d, n);
            float dotLN = math.dot(l, n);

            Assert.AreEqual(0f, dotDL, 1e-5f, "Direction and Lateral should be orthogonal");
            Assert.AreEqual(0f, dotDN, 1e-5f, "Direction and Normal should be orthogonal");
            Assert.AreEqual(0f, dotLN, 1e-5f, "Lateral and Normal should be orthogonal");

            Assert.AreEqual(1f, math.length(d), 1e-5f, "Direction should be unit length");
            Assert.AreEqual(1f, math.length(l), 1e-5f, "Lateral should be unit length");
            Assert.AreEqual(1f, math.length(n), 1e-5f, "Normal should be unit length");
        }

        [Test]
        public void Build_ZeroArcAndFriction() {
            float3 position = float3.zero;

            AnchorNode.Build(in position, 0f, 0f, 0f, 1f, 10f, 1.1f, 0f, 0f, out Point result);

            Assert.AreEqual(0f, result.HeartArc, 1e-5f);
            Assert.AreEqual(0f, result.SpineArc, 1e-5f);
            Assert.AreEqual(0f, result.FrictionOrigin, 1e-5f);
        }

        [Test]
        public void Build_StoresPhysicsParams() {
            float3 position = float3.zero;

            AnchorNode.Build(in position, 0f, 0f, 0f, 10f, 100f, 1.5f, 0.02f, 0.001f, out Point result);

            Assert.AreEqual(0f, result.RollSpeed, 1e-5f);
            Assert.AreEqual(1.5f, result.HeartOffset, 1e-5f);
            Assert.AreEqual(0.02f, result.Friction, 1e-5f);
            Assert.AreEqual(0.001f, result.Resistance, 1e-5f);
        }
    }
}
