using KexEdit.Core;
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
            Assert.AreEqual(1e-3f, Sim.MIN_VELOCITY, TOLERANCE);
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
        public void ComputeTotalEnergy_AtRest_OnlyPotential() {
            float velocity = 0f;
            float centerY = 10f;
            float frictionDistance = 0f;
            float friction = 0f;

            float energy = Sim.ComputeTotalEnergy(velocity, centerY, frictionDistance, friction);
            float expectedPE = Sim.G * centerY;

            Assert.AreEqual(expectedPE, energy, TOLERANCE, "At rest, energy = potential energy");
        }

        [Test]
        public void ComputeTotalEnergy_Moving_IncludesKinetic() {
            float velocity = 10f;
            float centerY = 0f;
            float frictionDistance = 0f;
            float friction = 0f;

            float energy = Sim.ComputeTotalEnergy(velocity, centerY, frictionDistance, friction);
            float expectedKE = 0.5f * velocity * velocity;

            Assert.AreEqual(expectedKE, energy, TOLERANCE, "At ground level, energy = kinetic energy");
        }

        [Test]
        public void ComputeTotalEnergy_WithFriction_AddsFrictionTerm() {
            float velocity = 10f;
            float centerY = 5f;
            float frictionDistance = 100f;
            float friction = 0.02f;

            float energy = Sim.ComputeTotalEnergy(velocity, centerY, frictionDistance, friction);
            float expectedKE = 0.5f * velocity * velocity;
            float expectedPE = Sim.G * centerY;
            float expectedFriction = Sim.G * frictionDistance * friction;

            Assert.AreEqual(expectedKE + expectedPE + expectedFriction, energy, TOLERANCE);
        }

        [Test]
        public void UpdateEnergy_WithResistance_EnergyDecreases() {
            float prevEnergy = 500f;
            float prevVelocity = 20f;
            float centerY = 0f;
            float frictionDistance = 0f;
            float friction = 0f;
            float resistance = 0.001f;

            Sim.UpdateEnergy(prevEnergy, prevVelocity, centerY, frictionDistance, friction, resistance,
                out float newEnergy, out _);

            Assert.Less(newEnergy, prevEnergy, "Energy should decrease with resistance");

            Sim.UpdateEnergy(prevEnergy, prevVelocity, centerY, frictionDistance, friction, 0f,
                out float energyNoResistance, out float velocityNoResistance);
            Sim.UpdateEnergy(prevEnergy, prevVelocity, centerY, frictionDistance, friction, resistance,
                out float energyWithResistance, out float velocityWithResistance);

            Assert.Less(velocityWithResistance, velocityNoResistance, "Resistance should reduce velocity");
        }

        [Test]
        public void UpdateEnergy_ZeroResistance_ConservesEnergy() {
            float prevEnergy = 500f;
            float prevVelocity = 20f;
            float centerY = 0f;
            float frictionDistance = 0f;
            float friction = 0f;
            float resistance = 0f;

            Sim.UpdateEnergy(prevEnergy, prevVelocity, centerY, frictionDistance, friction, resistance,
                out float newEnergy, out float newVelocity);

            Assert.AreEqual(prevEnergy, newEnergy, TOLERANCE, "Energy should be conserved with zero resistance");
        }

        [Test]
        public void UpdateEnergy_HigherAltitude_LowerVelocity() {
            float energy = 500f;
            float velocity = 20f;
            float lowCenterY = 0f;
            float highCenterY = 10f;
            float resistance = 0f;

            Sim.UpdateEnergy(energy, velocity, lowCenterY, 0f, 0f, resistance, out _, out float velocityLow);
            Sim.UpdateEnergy(energy, velocity, highCenterY, 0f, 0f, resistance, out _, out float velocityHigh);

            Assert.Greater(velocityLow, velocityHigh, "Higher altitude should yield lower velocity for same energy");
        }

        [Test]
        public void UpdateEnergy_InsufficientEnergy_VelocityBecomesZero() {
            float prevEnergy = 50f;
            float prevVelocity = 5f;
            float centerY = 100f;
            float frictionDistance = 0f;
            float friction = 0f;
            float resistance = 0f;

            Sim.UpdateEnergy(prevEnergy, prevVelocity, centerY, frictionDistance, friction, resistance,
                out float newEnergy, out float newVelocity);

            Assert.AreEqual(0f, newVelocity, TOLERANCE, "Velocity should clamp to 0 when energy insufficient");
        }

        [Test]
        public void UpdateEnergy_VelocityCubedResistance() {
            float prevEnergy = 1000f;
            float slowVelocity = 5f;
            float fastVelocity = 20f;
            float resistance = 0.001f;

            Sim.UpdateEnergy(prevEnergy, slowVelocity, 0f, 0f, 0f, resistance, out float slowEnergy, out _);
            Sim.UpdateEnergy(prevEnergy, fastVelocity, 0f, 0f, 0f, resistance, out float fastEnergy, out _);

            float slowLoss = prevEnergy - slowEnergy;
            float fastLoss = prevEnergy - fastEnergy;

            Assert.Greater(fastLoss, slowLoss, "Higher velocity should lose more energy");
            float expectedRatio = (fastVelocity * fastVelocity * fastVelocity) / (slowVelocity * slowVelocity * slowVelocity);
            float actualRatio = fastLoss / slowLoss;
            Assert.AreEqual(expectedRatio, actualRatio, 2f, "Energy loss should roughly scale with v^3");
        }
    }
}
