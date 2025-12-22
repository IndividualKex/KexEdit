using KexEdit.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class CoreCurvatureTests {
        private const float TOLERANCE = 1e-5f;

        [Test]
        public void Zero_HasZeroAngles() {
            Curvature zero = Curvature.Zero;
            Assert.AreEqual(0f, zero.DeltaPitch, TOLERANCE);
            Assert.AreEqual(0f, zero.DeltaYaw, TOLERANCE);
            Assert.AreEqual(0f, zero.TotalAngle, TOLERANCE);
            Assert.AreEqual(1f, zero.YawScale, TOLERANCE);
        }

        [Test]
        public void FromFrames_IdenticalFrames_ZeroCurvature() {
            Frame frame = Frame.Default;
            Curvature curvature = Curvature.FromFrames(frame, frame);

            Assert.AreEqual(0f, curvature.DeltaPitch, TOLERANCE);
            Assert.AreEqual(0f, curvature.DeltaYaw, TOLERANCE);
            Assert.AreEqual(0f, curvature.TotalAngle, TOLERANCE);
        }

        [Test]
        public void FromFrames_PurePitch_OnlyDeltaPitchNonZero() {
            Frame prev = Frame.Default;
            Frame curr = prev.WithPitch(0.1f);
            Curvature curvature = Curvature.FromFrames(curr, prev);

            Assert.AreNotEqual(0f, curvature.DeltaPitch, "DeltaPitch should be non-zero");
            Assert.AreEqual(0f, curvature.DeltaYaw, TOLERANCE, "DeltaYaw should be zero for pure pitch");
        }

        [Test]
        public void FromFrames_PureYaw_OnlyDeltaYawNonZero() {
            Frame prev = Frame.Default;
            Frame curr = prev.WithYaw(0.1f);
            Curvature curvature = Curvature.FromFrames(curr, prev);

            Assert.AreNotEqual(0f, curvature.DeltaYaw, "DeltaYaw should be non-zero");
            Assert.AreEqual(0f, curvature.DeltaPitch, TOLERANCE, "DeltaPitch should be zero for pure yaw");
        }

        [Test]
        public void FromFrames_TotalAngle_CombinesPitchAndYaw() {
            Frame prev = Frame.Default;
            Frame pitched = prev.WithPitch(0.05f);
            Frame curr = pitched.WithYaw(0.05f);
            Curvature curvature = Curvature.FromFrames(curr, prev);

            Assert.Greater(curvature.TotalAngle, 0f, "TotalAngle should be positive");
            Assert.Greater(curvature.TotalAngle, math.abs(curvature.DeltaPitch),
                "TotalAngle should be greater than pitch alone");
        }

        [Test]
        public void FromFrames_YawScale_DependsOnPitch() {
            Frame flat = Frame.Default;
            Frame pitched = Frame.FromEuler(0.5f, 0f, 0f);

            Curvature curvFlat = Curvature.FromFrames(flat, flat);
            Curvature curvPitched = Curvature.FromFrames(pitched, pitched);

            Assert.AreEqual(1f, curvFlat.YawScale, TOLERANCE, "YawScale should be 1 at zero pitch");
            Assert.Less(curvPitched.YawScale, 1f, "YawScale should decrease with pitch");
        }

        [Test]
        public void FromFrames_AngleWrapping_HandlesNearPi() {
            Frame prev = Frame.FromEuler(0f, math.PI - 0.05f, 0f);
            Frame curr = Frame.FromEuler(0f, -math.PI + 0.05f, 0f);
            Curvature curvature = Curvature.FromFrames(curr, prev);

            Assert.Less(math.abs(curvature.DeltaYaw), 0.2f,
                "DeltaYaw should wrap correctly around PI boundary");
        }
    }

    [TestFixture]
    [Category("Unit")]
    public class CoreForcesTests {
        private const float TOLERANCE = 1e-4f;

        [Test]
        public void OneG_HasUnitNormal() {
            Forces oneG = Forces.OneG;
            Assert.AreEqual(1f, oneG.Normal, TOLERANCE);
            Assert.AreEqual(0f, oneG.Lateral, TOLERANCE);
        }

        [Test]
        public void Zero_HasNoForces() {
            Forces zero = Forces.Zero;
            Assert.AreEqual(0f, zero.Normal, TOLERANCE);
            Assert.AreEqual(0f, zero.Lateral, TOLERANCE);
        }

        [Test]
        public void Compute_ZeroCurvature_OneGNormalZeroLateral() {
            Curvature curvature = Curvature.Zero;
            Frame frame = Frame.Default;
            Forces forces = Forces.Compute(curvature, frame, 10f, 0.1f);

            Assert.AreEqual(1f, forces.Normal, TOLERANCE, "Normal force should be 1G on flat track");
            Assert.AreEqual(0f, forces.Lateral, TOLERANCE, "Lateral force should be 0 on flat track");
        }

        [Test]
        public void Compute_FlatTrack_ApproximatelyOneG() {
            Frame prev = Frame.Default;
            Frame curr = Frame.Default;
            Curvature curvature = Curvature.FromFrames(curr, prev);
            Forces forces = Forces.Compute(curvature, curr, 15f, 0.15f);

            Assert.AreEqual(1f, forces.Normal, TOLERANCE, "Normal should be ~1G on flat track");
            Assert.AreEqual(0f, forces.Lateral, TOLERANCE, "Lateral should be ~0 on flat track");
        }

        [Test]
        public void Compute_VerticalLoop_HighNormalForce() {
            Frame prev = Frame.Default;
            Frame curr = prev.WithPitch(0.1f);
            Curvature curvature = Curvature.FromFrames(curr, prev);
            Forces forces = Forces.Compute(curvature, curr, 20f, 0.2f);

            Assert.Greater(forces.Normal, 1f, "Normal force should be > 1G in vertical curve");
        }

        [Test]
        public void Compute_BankedCurve_LateralForcePresent() {
            Frame prev = Frame.FromDirectionAndRoll(math.back(), 0.3f);
            Frame curr = prev.WithYaw(0.1f);
            Curvature curvature = Curvature.FromFrames(curr, prev);
            Forces forces = Forces.Compute(curvature, curr, 15f, 0.15f);

            Assert.AreNotEqual(0f, forces.Lateral, "Lateral force should be non-zero in banked curve");
        }

        [Test]
        public void Compute_ZeroVelocity_HandlesGracefully() {
            Frame prev = Frame.Default;
            Frame curr = prev.WithPitch(0.05f);
            Curvature curvature = Curvature.FromFrames(curr, prev);

            Assert.DoesNotThrow(() => {
                Forces.Compute(curvature, curr, 0f, 0f);
            }, "Should handle zero velocity without throwing");
        }
    }
}
