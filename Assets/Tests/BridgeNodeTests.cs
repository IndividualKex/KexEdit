using KexEdit.Core;
using KexEdit.Nodes.Bridge;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Keyframe = KexEdit.Core.Keyframe;

namespace Tests {
    [BurstCompile]
    struct BridgeNodeJob : IJob {
        [ReadOnly] public Point Anchor;
        [ReadOnly] public Point TargetAnchor;
        public float InWeight;
        public float OutWeight;
        public bool FixedVelocity;
        [ReadOnly] public NativeArray<Keyframe> FixedVelocityKeyframes;
        [ReadOnly] public NativeArray<Keyframe> HeartOffset;
        [ReadOnly] public NativeArray<Keyframe> Friction;
        [ReadOnly] public NativeArray<Keyframe> Resistance;
        public float AnchorHeart;
        public float AnchorFriction;
        public float AnchorResistance;
        public NativeList<Point> Result;

        public void Execute() {
            BridgeNode.Build(
                in Anchor, in TargetAnchor, InWeight, OutWeight, FixedVelocity,
                in FixedVelocityKeyframes, in HeartOffset, in Friction, in Resistance,
                AnchorHeart, AnchorFriction, AnchorResistance,
                ref Result
            );
        }
    }

    [TestFixture]
    [Category("Golden")]
    public class BridgeNodeTests {
        private static void RunBridgeNode(in BridgeTestData data, ref NativeList<Point> result) {
            new BridgeNodeJob {
                Anchor = data.Anchor,
                TargetAnchor = data.TargetAnchor,
                InWeight = data.InWeight,
                OutWeight = data.OutWeight,
                FixedVelocity = data.FixedVelocity,
                FixedVelocityKeyframes = data.FixedVelocityKeyframes,
                HeartOffset = data.HeartOffset,
                Friction = data.Friction,
                Resistance = data.Resistance,
                AnchorHeart = data.AnchorHeart,
                AnchorFriction = data.AnchorFriction,
                AnchorResistance = data.AnchorResistance,
                Result = result
            }.Schedule().Complete();
        }

        [Test]
        public void AllTypes_BridgeSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var bridgeSections = GoldDataLoader.GetBridgeSections(gold);

            if (bridgeSections.Count == 0) {
                Assert.Ignore("No Bridge section found in all_types.json. Export gold data with a bridge section first.");
                return;
            }

            var section = bridgeSections[0];
            var data = BridgeTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunBridgeNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }
    }
}
