using KexEdit.Sim;
using KexEdit.Sim.Nodes.Bridge;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Safety")]
    public class BridgeNodeLimitsTests {
        [Test]
        public void Bridge_VeryLongPath_CapsAtMaxPoints() {
            // Create a very long bridge path that would normally produce hundreds of thousands of points
            var anchor = Point.Create(
                heartPosition: new float3(0f, 100f, 0f),
                direction: math.forward(),
                roll: 0f,
                velocity: 0.5f, // Very slow velocity = many points per meter
                heartOffset: 1.1f
            );

            var target = Point.Create(
                heartPosition: new float3(0f, 0f, 1000f), // 1km bridge
                direction: math.forward(),
                roll: 0f,
                velocity: 0.5f,
                heartOffset: 1.1f
            );

            var result = new NativeList<Point>(Allocator.TempJob);
            var emptyKeyframes = new NativeArray<Keyframe>(0, Allocator.TempJob);

            try {
                BridgeNode.Build(
                    in anchor,
                    in target,
                    inWeight: 0.3f,
                    outWeight: 0.3f,
                    driven: false,
                    in emptyKeyframes,
                    in emptyKeyframes,
                    in emptyKeyframes,
                    in emptyKeyframes,
                    anchorHeart: 1.1f,
                    anchorFriction: 0.02f,
                    anchorResistance: 0.00002f,
                    ref result
                );

                // Should not exceed 50,000 points (MAX_POINTS constant in BridgeNode)
                Assert.LessOrEqual(result.Length, 50_000,
                    "Bridge should cap at MAX_POINTS to prevent degenerate cases");

                // Should still produce a reasonable number of points
                Assert.Greater(result.Length, 100,
                    "Bridge should still produce points up to the limit");
            }
            finally {
                result.Dispose();
                emptyKeyframes.Dispose();
            }
        }

        [Test]
        public void Bridge_NormalPath_ProducesReasonablePoints() {
            // Normal bridge that should complete without hitting limits
            var anchor = Point.Create(
                heartPosition: new float3(0f, 10f, 0f),
                direction: math.forward(),
                roll: 0f,
                velocity: 20f,
                heartOffset: 1.1f
            );

            var target = Point.Create(
                heartPosition: new float3(0f, 10f, 50f), // 50m bridge
                direction: math.forward(),
                roll: 0f,
                velocity: 20f,
                heartOffset: 1.1f
            );

            var result = new NativeList<Point>(Allocator.TempJob);
            var emptyKeyframes = new NativeArray<Keyframe>(0, Allocator.TempJob);

            try {
                BridgeNode.Build(
                    in anchor,
                    in target,
                    inWeight: 0.3f,
                    outWeight: 0.3f,
                    driven: false,
                    in emptyKeyframes,
                    in emptyKeyframes,
                    in emptyKeyframes,
                    in emptyKeyframes,
                    anchorHeart: 1.1f,
                    anchorFriction: 0.02f,
                    anchorResistance: 0.00002f,
                    ref result
                );

                // Should complete normally with reasonable point count
                // At 20 m/s and 100 Hz, 50m should take about 250 points
                Assert.Greater(result.Length, 100, "Bridge should produce reasonable points");
                Assert.Less(result.Length, 1000, "Bridge should not produce excessive points for short path");
            }
            finally {
                result.Dispose();
                emptyKeyframes.Dispose();
            }
        }
    }
}
