using KexEdit.Core;
using KexEdit.Nodes;
using KexEdit.Nodes.Force;
using KexEdit.Native.RustCore;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Keyframe = KexEdit.Core.Keyframe;

namespace Tests {
    [TestFixture]
    [Category("Validation")]
    public class RustForceNodeValidationTests {
        private const float BASE_TOLERANCE = 1e-3f;
        private const float MACHINE_EPSILON = 1.1920929e-7f;
        private const float TOLERANCE_PER_STEP = MACHINE_EPSILON * 1024f;

        [Test]
        public void RustMatchesBurst_SimpleTimeSection() {
            var anchor = Point.Create(
                spinePosition: new float3(0f, 3f, 0f),
                direction: math.back(),
                roll: 0f,
                velocity: 10f,
                heartOffset: 1.1f,
                friction: 0f,
                resistance: 0f
            );

            var config = new IterationConfig(0.5f, DurationType.Time);
            var emptyKeyframes = new NativeArray<Keyframe>(0, Allocator.Temp);

            var burstResult = new NativeList<Point>(Allocator.Temp);
            ForceNode.Build(
                in anchor, in config, false,
                emptyKeyframes, emptyKeyframes, emptyKeyframes,
                emptyKeyframes, emptyKeyframes, emptyKeyframes, emptyKeyframes,
                1.1f, 0f, 0f,
                ref burstResult
            );

            var rustResult = new NativeList<Point>(Allocator.Temp);
            RustForceNode.Build(
                in anchor, 0.5f, (int)DurationType.Time, false,
                emptyKeyframes, emptyKeyframes, emptyKeyframes,
                emptyKeyframes, emptyKeyframes, emptyKeyframes, emptyKeyframes,
                1.1f, 0f, 0f,
                ref rustResult
            );

            Assert.AreEqual(burstResult.Length, rustResult.Length, "Point count mismatch");

            for (int i = 0; i < burstResult.Length; i++) {
                AssertPointsMatch(burstResult[i], rustResult[i], i);
            }

            burstResult.Dispose();
            rustResult.Dispose();
            emptyKeyframes.Dispose();
        }

        [Test]
        public void RustMatchesBurst_SimpleDistanceSection() {
            var anchor = Point.Create(
                spinePosition: new float3(0f, 3f, 0f),
                direction: math.back(),
                roll: 0f,
                velocity: 10f,
                heartOffset: 1.1f,
                friction: 0f,
                resistance: 0f
            );

            var config = new IterationConfig(5.0f, DurationType.Distance);
            var emptyKeyframes = new NativeArray<Keyframe>(0, Allocator.Temp);

            var burstResult = new NativeList<Point>(Allocator.Temp);
            ForceNode.Build(
                in anchor, in config, false,
                emptyKeyframes, emptyKeyframes, emptyKeyframes,
                emptyKeyframes, emptyKeyframes, emptyKeyframes, emptyKeyframes,
                1.1f, 0f, 0f,
                ref burstResult
            );

            var rustResult = new NativeList<Point>(Allocator.Temp);
            RustForceNode.Build(
                in anchor, 5.0f, (int)DurationType.Distance, false,
                emptyKeyframes, emptyKeyframes, emptyKeyframes,
                emptyKeyframes, emptyKeyframes, emptyKeyframes, emptyKeyframes,
                1.1f, 0f, 0f,
                ref rustResult
            );

            Assert.AreEqual(burstResult.Length, rustResult.Length, "Point count mismatch");

            for (int i = 0; i < burstResult.Length; i++) {
                AssertPointsMatch(burstResult[i], rustResult[i], i);
            }

            burstResult.Dispose();
            rustResult.Dispose();
            emptyKeyframes.Dispose();
        }

        [Test]
        public void RustMatchesBurst_AllTypes_ForceSection() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);
            var data = ForceTestBuilder.FromGold(section, Allocator.Temp);

            var burstResult = new NativeList<Point>(Allocator.Temp);
            ForceNode.Build(
                in data.Anchor, in data.Config, data.FixedVelocity,
                data.RollSpeed, data.NormalForce, data.LateralForce,
                data.FixedVelocityKeyframes, data.HeartOffset, data.Friction, data.Resistance,
                data.AnchorHeart, data.AnchorFriction, data.AnchorResistance,
                ref burstResult
            );

            var rustResult = new NativeList<Point>(Allocator.Temp);
            RustForceNode.Build(
                in data.Anchor, data.Config.Duration, (int)data.Config.DurationType, data.FixedVelocity,
                data.RollSpeed, data.NormalForce, data.LateralForce,
                data.FixedVelocityKeyframes, data.HeartOffset, data.Friction, data.Resistance,
                data.AnchorHeart, data.AnchorFriction, data.AnchorResistance,
                ref rustResult
            );

            Assert.AreEqual(burstResult.Length, rustResult.Length, "Point count mismatch");

            for (int i = 0; i < burstResult.Length; i++) {
                AssertPointsMatch(burstResult[i], rustResult[i], i);
            }

            burstResult.Dispose();
            rustResult.Dispose();
            data.Dispose();
        }

        private void AssertPointsMatch(Point burst, Point rust, int index) {
            float tolerance = BASE_TOLERANCE + TOLERANCE_PER_STEP * index;

            AssertFloat3Match(burst.SpinePosition, rust.SpinePosition, "SpinePosition", index, tolerance);
            AssertFloat3Match(burst.Direction, rust.Direction, "Direction", index, tolerance);
            AssertFloat3Match(burst.Normal, rust.Normal, "Normal", index, tolerance);
            AssertFloat3Match(burst.Lateral, rust.Lateral, "Lateral", index, tolerance);

            AssertScalarMatch(burst.Velocity, rust.Velocity, "Velocity", index, tolerance);
            AssertScalarMatch(burst.Energy, rust.Energy, "Energy", index, tolerance);
            AssertScalarMatch(burst.NormalForce, rust.NormalForce, "NormalForce", index, tolerance);
            AssertScalarMatch(burst.LateralForce, rust.LateralForce, "LateralForce", index, tolerance);
            AssertScalarMatch(burst.HeartArc, rust.HeartArc, "HeartArc", index, tolerance);
            AssertScalarMatch(burst.SpineArc, rust.SpineArc, "SpineArc", index, tolerance);
        }

        private void AssertFloat3Match(float3 burst, float3 rust, string fieldName, int index, float tolerance) {
            AssertScalarMatch(burst.x, rust.x, $"{fieldName}.x", index, tolerance);
            AssertScalarMatch(burst.y, rust.y, $"{fieldName}.y", index, tolerance);
            AssertScalarMatch(burst.z, rust.z, $"{fieldName}.z", index, tolerance);
        }

        private void AssertScalarMatch(float burst, float rust, string fieldName, int index, float tolerance) {
            float error = math.abs(burst - rust);
            Assert.LessOrEqual(error, tolerance,
                $"Point[{index}].{fieldName}: expected {burst:e}, got {rust:e}, diff {error:e} (tolerance {tolerance:e})");
        }
    }
}
