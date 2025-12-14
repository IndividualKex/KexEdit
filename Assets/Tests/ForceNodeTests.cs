using KexEdit.Core;
using KexEdit.Nodes;
using KexEdit.Nodes.Force;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Keyframe = KexEdit.Core.Keyframe;

namespace Tests {
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

    [TestFixture]
    [Category("Golden")]
    public class ForceNodeTests {
        private static void RunForceNode(in ForceTestData data, ref NativeList<Point> result) {
            new ForceNodeJob {
                Anchor = data.Anchor,
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
        }

        [Test]
        public void Shuttle_ForceSection_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/shuttle.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var data = ForceTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunForceNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Veloci_ForceSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var data = ForceTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunForceNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Veloci_ForceSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 1);

            var data = ForceTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunForceNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void AllTypes_ForceSection_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var data = ForceTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunForceNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }
    }
}
