using KexEdit.Core;
using KexEdit.Nodes.CopyPath;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Keyframe = KexEdit.Core.Keyframe;

namespace Tests {
    [BurstCompile]
    struct CopyPathNodeJob : IJob {
        [ReadOnly] public Point Anchor;
        [ReadOnly] public NativeArray<Point> SourcePath;
        public float Start;
        public float End;
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
            CopyPathNode.Build(
                in Anchor, in SourcePath, Start, End, FixedVelocity,
                in FixedVelocityKeyframes, in HeartOffset, in Friction, in Resistance,
                AnchorHeart, AnchorFriction, AnchorResistance,
                ref Result
            );
        }
    }

    [TestFixture]
    [Category("Golden")]
    public class CopyPathNodeTests {
        private static void RunCopyPathNode(in CopyPathTestData data, ref NativeList<Point> result) {
            new CopyPathNodeJob {
                Anchor = data.Anchor,
                SourcePath = data.SourcePath,
                Start = data.Start,
                End = data.End,
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
        public void AllTypes_CopyPathSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetCopyPathSectionByIndex(gold, 0);

            var data = CopyPathTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunCopyPathNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void AllTypes_CopyPathSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetCopyPathSectionByIndex(gold, 1);

            var data = CopyPathTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunCopyPathNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void AllTypes_CopyPathSection3_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetCopyPathSectionByIndex(gold, 2);

            var data = CopyPathTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunCopyPathNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }
    }
}
