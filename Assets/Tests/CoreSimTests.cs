using KexEdit.Sim;
using NUnit.Framework;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class CoreSimTests {
        private const float TOLERANCE = 1e-4f;

        [Test]
        public void Constants_GravityMatchesStandard() {
            Assert.AreEqual(9.80665f, Sim.G, TOLERANCE, "G should be standard gravity");
        }

        [Test]
        public void Constants_SimulationRate() {
            Assert.AreEqual(100f, Sim.HZ, TOLERANCE, "HZ should be 100");
        }

        [Test]
        public void DT_EqualsOneOverHZ() {
            Assert.AreEqual(1f / Sim.HZ, Sim.DT, TOLERANCE, "DT should equal 1/HZ");
            Assert.AreEqual(0.01f, Sim.DT, TOLERANCE, "DT should be 0.01 seconds");
        }

        [Test]
        public void Constants_MinVelocity() {
            Assert.AreEqual(0.1f, Sim.MIN_VELOCITY, TOLERANCE);
        }

        [Test]
        public void WrapAngle_InRange_Unchanged() {
            float[] angles = { 0f, 0.5f, -0.5f, math.PI - 0.1f, -math.PI + 0.1f };
            foreach (float angle in angles) {
                float wrapped = Sim.WrapAngle(angle);
                Assert.AreEqual(angle, wrapped, TOLERANCE, $"Angle {angle} should be unchanged");
            }
        }

        [Test]
        public void WrapAngle_GreaterThanPi_WrapsNegative() {
            float angle = math.PI + 0.5f;
            float wrapped = Sim.WrapAngle(angle);

            Assert.Greater(wrapped, -math.PI, "Should be > -PI");
            Assert.LessOrEqual(wrapped, math.PI, "Should be <= PI");
            Assert.Less(wrapped, 0f, "Should wrap to negative");
        }

        [Test]
        public void WrapAngle_LessThanNegativePi_WrapsPositive() {
            float angle = -math.PI - 0.5f;
            float wrapped = Sim.WrapAngle(angle);

            Assert.Greater(wrapped, -math.PI, "Should be > -PI");
            Assert.LessOrEqual(wrapped, math.PI, "Should be <= PI");
            Assert.Greater(wrapped, 0f, "Should wrap to positive");
        }

        [Test]
        public void WrapAngle_ExactlyPi_ReturnsValidRange() {
            float wrapped = Sim.WrapAngle(math.PI);
            Assert.GreaterOrEqual(wrapped, -math.PI);
            Assert.LessOrEqual(wrapped, math.PI);
        }

        [Test]
        public void WrapAngle_LargePositive_WrapsCorrectly() {
            float angle = 2f * math.PI + 0.3f;
            float wrapped = Sim.WrapAngle(angle);

            Assert.Greater(wrapped, -math.PI);
            Assert.LessOrEqual(wrapped, math.PI);
            Assert.AreEqual(0.3f, wrapped, 1e-4f);
        }

        [Test]
        public void WrapAngle_ModerateNegative_WrapsCorrectly() {
            float angle = -2f * math.PI - 0.3f;
            float wrapped = Sim.WrapAngle(angle);

            Assert.Greater(wrapped, -math.PI);
            Assert.LessOrEqual(wrapped, math.PI);
        }

        [Test]
        public void UpdateVelocity_WithResistance_VelocityDecreases() {
            float prevVelocity = 20f;
            float deltaY = 0f;
            float deltaDistance = 1f;
            float friction = 0f;
            float resistance = 0.001f;

            float newVelocity = Sim.UpdateVelocity(prevVelocity, deltaY, deltaDistance, friction, resistance);

            Assert.Less(newVelocity, prevVelocity, "Velocity should decrease with resistance");
        }

        [Test]
        public void UpdateVelocity_ZeroResistance_ConservesVelocity() {
            float prevVelocity = 20f;
            float deltaY = 0f;
            float deltaDistance = 0f;
            float friction = 0f;
            float resistance = 0f;

            float newVelocity = Sim.UpdateVelocity(prevVelocity, deltaY, deltaDistance, friction, resistance);

            Assert.AreEqual(prevVelocity, newVelocity, TOLERANCE, "Velocity should be conserved with zero resistance and no height change");
        }

        [Test]
        public void UpdateVelocity_GoingUp_VelocityDecreases() {
            float prevVelocity = 20f;
            float deltaY = 1f;  // Going up by 1 meter
            float deltaDistance = 0f;
            float friction = 0f;
            float resistance = 0f;

            float newVelocity = Sim.UpdateVelocity(prevVelocity, deltaY, deltaDistance, friction, resistance);

            Assert.Less(newVelocity, prevVelocity, "Velocity should decrease when going up");
        }

        [Test]
        public void UpdateVelocity_GoingDown_VelocityIncreases() {
            float prevVelocity = 20f;
            float deltaY = -1f;  // Going down by 1 meter
            float deltaDistance = 0f;
            float friction = 0f;
            float resistance = 0f;

            float newVelocity = Sim.UpdateVelocity(prevVelocity, deltaY, deltaDistance, friction, resistance);

            Assert.Greater(newVelocity, prevVelocity, "Velocity should increase when going down");
        }

        [Test]
        public void UpdateVelocity_InsufficientEnergy_VelocityBecomesZero() {
            float prevVelocity = 5f;
            float deltaY = 100f;  // Going way up
            float deltaDistance = 0f;
            float friction = 0f;
            float resistance = 0f;

            float newVelocity = Sim.UpdateVelocity(prevVelocity, deltaY, deltaDistance, friction, resistance);

            Assert.AreEqual(0f, newVelocity, TOLERANCE, "Velocity should clamp to 0 when going too high");
        }

        [Test]
        public void UpdateVelocity_VelocityCubedResistance() {
            float slowVelocity = 5f;
            float fastVelocity = 20f;
            float deltaY = 0f;
            float deltaDistance = 1f;
            float friction = 0f;
            float resistance = 0.001f;

            float slowResult = Sim.UpdateVelocity(slowVelocity, deltaY, deltaDistance, friction, resistance);
            float fastResult = Sim.UpdateVelocity(fastVelocity, deltaY, deltaDistance, friction, resistance);

            float slowLoss = slowVelocity - slowResult;
            float fastLoss = fastVelocity - fastResult;

            Assert.Greater(fastLoss, slowLoss, "Higher velocity should lose more velocity");
        }
    }
}
