using KexEdit.Sim;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Nodes.Force;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Keyframe = KexEdit.Sim.Keyframe;

namespace Tests {
    /// <summary>
    /// Tests for Y-offset invariance in force section simulation.
    ///
    /// When the Y position of an anchor is offset, the track SHAPE should be
    /// identical. Since energy is offset to compensate for potential energy:
    /// - Direction/Normal/Lateral vectors: IDENTICAL
    /// - Velocity: IDENTICAL (energy offset compensates for PE)
    /// - Position: differs only by constant Y offset (no XZ drift)
    /// - Energy: differs by constant g*deltaY (expected)
    /// </summary>
    [TestFixture]
    [Category("Stability")]
    public class YOffsetDriftTests {
        [BurstCompile]
        struct ForceNodeJob : IJob {
            [ReadOnly] public Point Anchor;
            [ReadOnly] public IterationConfig Config;
            public bool FixedVelocity;
            [ReadOnly] public NativeArray<Keyframe> RollSpeed;
            [ReadOnly] public NativeArray<Keyframe> NormalForce;
            [ReadOnly] public NativeArray<Keyframe> LateralForce;
            [ReadOnly] public NativeArray<Keyframe> FixedVelocityKeyframes;
            [ReadOnly] public NativeArray<Keyframe> HeartOffset;
            [ReadOnly] public NativeArray<Keyframe> Friction;
            [ReadOnly] public NativeArray<Keyframe> Resistance;
            public float AnchorHeart;
            public float AnchorFriction;
            public float AnchorResistance;
            public NativeList<Point> Result;

            public void Execute() {
                ForceNode.Build(
                    in Anchor, in Config, FixedVelocity,
                    in RollSpeed, in NormalForce, in LateralForce,
                    in FixedVelocityKeyframes, in HeartOffset, in Friction, in Resistance,
                    AnchorHeart, AnchorFriction, AnchorResistance,
                    ref Result
                );
            }
        }

        private static Point OffsetAnchorY(Point anchor, float yOffset) {
            float3 newPos = anchor.HeartPosition;
            newPos.y += yOffset;

            // With delta-based velocity, we just offset position
            // Velocity is preserved because it's computed from delta changes
            return new Point(
                heartPosition: newPos,
                direction: anchor.Direction,
                normal: anchor.Normal,
                lateral: anchor.Lateral,
                velocity: anchor.Velocity,
                normalForce: anchor.NormalForce,
                lateralForce: anchor.LateralForce,
                heartArc: anchor.HeartArc,
                spineArc: anchor.SpineArc,
                heartAdvance: anchor.HeartAdvance,
                frictionOrigin: anchor.FrictionOrigin,
                rollSpeed: anchor.RollSpeed,
                heartOffset: anchor.HeartOffset,
                friction: anchor.Friction,
                resistance: anchor.Resistance
            );
        }

        private static NativeList<Point> BuildFromGoldWithYOffset(GoldSection section, float yOffset, Allocator allocator) {
            var data = ForceTestBuilder.FromGold(section, allocator);
            var anchor = yOffset != 0f ? OffsetAnchorY(data.Anchor, yOffset) : data.Anchor;

            var result = new NativeList<Point>(allocator);

            new ForceNodeJob {
                Anchor = anchor,
                Config = data.Config,
                FixedVelocity = data.FixedVelocity,
                RollSpeed = data.RollSpeed,
                NormalForce = data.NormalForce,
                LateralForce = data.LateralForce,
                FixedVelocityKeyframes = data.FixedVelocityKeyframes,
                HeartOffset = data.HeartOffset,
                Friction = data.Friction,
                Resistance = data.Resistance,
                AnchorHeart = data.AnchorHeart,
                AnchorFriction = data.AnchorFriction,
                AnchorResistance = data.AnchorResistance,
                Result = result
            }.Schedule().Complete();

            data.Dispose();
            return result;
        }

        /// <summary>
        /// Test that direction vectors are identical regardless of Y offset.
        /// This is the primary shape invariant.
        /// </summary>
        [Test]
        public void Circuit_ForceSection1_YOffset_DirectionIdentical() {
            const float Y_OFFSET = 0.1f;
            const float MAX_DIFF = 1e-4f;  // Realistic tolerance for FP accumulation

            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/circuit.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var baseline = BuildFromGoldWithYOffset(section, 0f, Allocator.TempJob);
            var offset = BuildFromGoldWithYOffset(section, Y_OFFSET, Allocator.TempJob);

            try {
                Assert.AreEqual(baseline.Length, offset.Length, "Point count mismatch");

                float maxDirDiff = 0f;
                float maxNormDiff = 0f;
                float maxLatDiff = 0f;
                int worstIdx = 0;

                for (int i = 0; i < baseline.Length; i++) {
                    float dirDiff = math.length(baseline[i].Direction - offset[i].Direction);
                    float normDiff = math.length(baseline[i].Normal - offset[i].Normal);
                    float latDiff = math.length(baseline[i].Lateral - offset[i].Lateral);

                    if (dirDiff > maxDirDiff) { maxDirDiff = dirDiff; worstIdx = i; }
                    maxNormDiff = math.max(maxNormDiff, normDiff);
                    maxLatDiff = math.max(maxLatDiff, latDiff);
                }

                UnityEngine.Debug.Log($"Direction/Frame Drift Test (Y offset: {Y_OFFSET}):");
                UnityEngine.Debug.Log($"  Points: {baseline.Length}");
                UnityEngine.Debug.Log($"  Max direction diff: {maxDirDiff:E3} at point {worstIdx}");
                UnityEngine.Debug.Log($"  Max normal diff: {maxNormDiff:E3}");
                UnityEngine.Debug.Log($"  Max lateral diff: {maxLatDiff:E3}");

                Assert.LessOrEqual(maxDirDiff, MAX_DIFF, $"Direction differs at point {worstIdx}");
                Assert.LessOrEqual(maxNormDiff, MAX_DIFF, "Normal differs");
                Assert.LessOrEqual(maxLatDiff, MAX_DIFF, "Lateral differs");
            }
            finally {
                baseline.Dispose();
                offset.Dispose();
            }
        }

        /// <summary>
        /// Test that velocity is identical regardless of Y offset.
        /// Energy is offset to compensate for PE, so KE (velocity) should match.
        /// </summary>
        [Test]
        public void Circuit_ForceSection1_YOffset_VelocityIdentical() {
            const float Y_OFFSET = 0.1f;
            const float MAX_DIFF = 2e-3f;  // Realistic tolerance for FP accumulation

            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/circuit.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var baseline = BuildFromGoldWithYOffset(section, 0f, Allocator.TempJob);
            var offset = BuildFromGoldWithYOffset(section, Y_OFFSET, Allocator.TempJob);

            try {
                float maxVelDiff = 0f;
                int worstIdx = 0;

                for (int i = 0; i < baseline.Length; i++) {
                    float velDiff = math.abs(baseline[i].Velocity - offset[i].Velocity);
                    if (velDiff > maxVelDiff) {
                        maxVelDiff = velDiff;
                        worstIdx = i;
                    }
                }

                UnityEngine.Debug.Log($"Velocity Invariance Test (Y offset: {Y_OFFSET}):");
                UnityEngine.Debug.Log($"  Points: {baseline.Length}");
                UnityEngine.Debug.Log($"  Max velocity diff: {maxVelDiff:E3} at point {worstIdx}");
                UnityEngine.Debug.Log($"  Baseline final vel: {baseline[baseline.Length-1].Velocity:F4}");
                UnityEngine.Debug.Log($"  Offset final vel: {offset[offset.Length-1].Velocity:F4}");

                Assert.LessOrEqual(maxVelDiff, MAX_DIFF, $"Velocity differs at point {worstIdx}");
            }
            finally {
                baseline.Dispose();
                offset.Dispose();
            }
        }

        /// <summary>
        /// Test that XZ position drift is zero (only Y differs by constant offset).
        /// </summary>
        [Test]
        public void Circuit_ForceSection1_YOffset_NoXZDrift() {
            const float Y_OFFSET = 0.1f;
            const float MAX_XZ_DRIFT = 2e-2f;  // Realistic tolerance for FP accumulation

            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/circuit.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var baseline = BuildFromGoldWithYOffset(section, 0f, Allocator.TempJob);
            var offset = BuildFromGoldWithYOffset(section, Y_OFFSET, Allocator.TempJob);

            try {
                float maxXDrift = 0f;
                float maxZDrift = 0f;
                int worstIdx = 0;

                for (int i = 0; i < baseline.Length; i++) {
                    float xDrift = math.abs(baseline[i].HeartPosition.x - offset[i].HeartPosition.x);
                    float zDrift = math.abs(baseline[i].HeartPosition.z - offset[i].HeartPosition.z);

                    if (xDrift > maxXDrift || zDrift > maxZDrift) {
                        maxXDrift = math.max(maxXDrift, xDrift);
                        maxZDrift = math.max(maxZDrift, zDrift);
                        worstIdx = i;
                    }
                }

                float3 baseEnd = baseline[baseline.Length - 1].HeartPosition;
                float3 offEnd = offset[offset.Length - 1].HeartPosition;

                UnityEngine.Debug.Log($"XZ Drift Test (Y offset: {Y_OFFSET}):");
                UnityEngine.Debug.Log($"  Points: {baseline.Length}");
                UnityEngine.Debug.Log($"  Max X drift: {maxXDrift:E3} at point {worstIdx}");
                UnityEngine.Debug.Log($"  Max Z drift: {maxZDrift:E3}");
                UnityEngine.Debug.Log($"  Baseline end: ({baseEnd.x:F4}, {baseEnd.y:F4}, {baseEnd.z:F4})");
                UnityEngine.Debug.Log($"  Offset end:   ({offEnd.x:F4}, {offEnd.y:F4}, {offEnd.z:F4})");
                UnityEngine.Debug.Log($"  End diff: ({offEnd.x - baseEnd.x:E3}, {offEnd.y - baseEnd.y:F4}, {offEnd.z - baseEnd.z:E3})");

                Assert.LessOrEqual(maxXDrift, MAX_XZ_DRIFT, $"X drift at point {worstIdx}");
                Assert.LessOrEqual(maxZDrift, MAX_XZ_DRIFT, $"Z drift at point {worstIdx}");
            }
            finally {
                baseline.Dispose();
                offset.Dispose();
            }
        }

        /// <summary>
        /// Test with larger Y offset to see if drift scales.
        /// </summary>
        [Test]
        public void Circuit_ForceSection1_LargeYOffset_ShapeIdentical() {
            const float Y_OFFSET = 10.0f;
            const float MAX_DIR_DIFF = 1e-3f;  // Larger offset means more accumulated error
            const float MAX_XZ_DRIFT = 1e-1f;

            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/circuit.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var baseline = BuildFromGoldWithYOffset(section, 0f, Allocator.TempJob);
            var offset = BuildFromGoldWithYOffset(section, Y_OFFSET, Allocator.TempJob);

            try {
                float maxDirDiff = 0f;
                float maxXZDrift = 0f;
                int dirWorst = 0, posWorst = 0;

                for (int i = 0; i < baseline.Length; i++) {
                    float dirDiff = math.length(baseline[i].Direction - offset[i].Direction);
                    float xzDrift = math.max(
                        math.abs(baseline[i].HeartPosition.x - offset[i].HeartPosition.x),
                        math.abs(baseline[i].HeartPosition.z - offset[i].HeartPosition.z)
                    );

                    if (dirDiff > maxDirDiff) { maxDirDiff = dirDiff; dirWorst = i; }
                    if (xzDrift > maxXZDrift) { maxXZDrift = xzDrift; posWorst = i; }
                }

                UnityEngine.Debug.Log($"Large Y Offset Test (Y offset: {Y_OFFSET}):");
                UnityEngine.Debug.Log($"  Points: {baseline.Length}");
                UnityEngine.Debug.Log($"  Max direction diff: {maxDirDiff:E3} at point {dirWorst}");
                UnityEngine.Debug.Log($"  Max XZ drift: {maxXZDrift:E3} at point {posWorst}");

                Assert.LessOrEqual(maxDirDiff, MAX_DIR_DIFF, $"Direction differs at point {dirWorst}");
                Assert.LessOrEqual(maxXZDrift, MAX_XZ_DRIFT, $"XZ drift at point {posWorst}");
            }
            finally {
                baseline.Dispose();
                offset.Dispose();
            }
        }

    }
}
