using KexEdit.Core;
using KexEdit.Nodes;
using KexEdit.Nodes.Curved;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Keyframe = KexEdit.Core.Keyframe;

namespace Tests {
    [BurstCompile]
    struct CurvedNodeJob : IJob {
        [ReadOnly] public Point Anchor;
        public float Radius;
        public float Arc;
        public float Axis;
        public float LeadIn;
        public float LeadOut;
        public bool FixedVelocity;
        [ReadOnly] public NativeArray<Keyframe> RollSpeed;
        [ReadOnly] public NativeArray<Keyframe> FixedVelocityKeyframes;
        [ReadOnly] public NativeArray<Keyframe> HeartOffset;
        [ReadOnly] public NativeArray<Keyframe> Friction;
        [ReadOnly] public NativeArray<Keyframe> Resistance;
        public float AnchorHeart;
        public float AnchorFriction;
        public float AnchorResistance;
        public NativeList<Point> Result;

        public void Execute() {
            CurvedNode.Build(
                in Anchor, Radius, Arc, Axis, LeadIn, LeadOut, FixedVelocity,
                in RollSpeed, in FixedVelocityKeyframes,
                in HeartOffset, in Friction, in Resistance,
                AnchorHeart, AnchorFriction, AnchorResistance,
                ref Result
            );
        }
    }

    [TestFixture]
    [Category("Golden")]
    public class CurvedNodeTests {
        private static void RunCurvedNode(in CurvedTestData data, ref NativeList<Point> result) {
            new CurvedNodeJob {
                Anchor = data.Anchor,
                Radius = data.Radius,
                Arc = data.Arc,
                Axis = data.Axis,
                LeadIn = data.LeadIn,
                LeadOut = data.LeadOut,
                FixedVelocity = data.FixedVelocity,
                RollSpeed = data.RollSpeed,
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
        public void Veloci_CurvedSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetCurvedSectionByIndex(gold, 0);

            var data = CurvedTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunCurvedNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }
    }
}
