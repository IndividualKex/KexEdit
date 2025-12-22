using KexEdit.Core;
using KexEdit.Nodes;
using KexEdit.Nodes.Geometric;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Keyframe = KexEdit.Core.Keyframe;

namespace Tests {
    [BurstCompile]
    struct GeometricNodeJob : IJob {
        [ReadOnly] public Point Anchor;
        [ReadOnly] public IterationConfig Config;
        public bool FixedVelocity;
        public bool Steering;
        [ReadOnly] public NativeArray<Keyframe> RollSpeed;
        [ReadOnly] public NativeArray<Keyframe> PitchSpeed;
        [ReadOnly] public NativeArray<Keyframe> YawSpeed;
        [ReadOnly] public NativeArray<Keyframe> FixedVelocityKeyframes;
        [ReadOnly] public NativeArray<Keyframe> HeartOffset;
        [ReadOnly] public NativeArray<Keyframe> Friction;
        [ReadOnly] public NativeArray<Keyframe> Resistance;
        public float AnchorHeart;
        public float AnchorFriction;
        public float AnchorResistance;
        public NativeList<Point> Result;

        public void Execute() {
            GeometricNode.Build(
                in Anchor, in Config, FixedVelocity, Steering,
                in RollSpeed, in PitchSpeed, in YawSpeed,
                in FixedVelocityKeyframes, in HeartOffset, in Friction, in Resistance,
                AnchorHeart, AnchorFriction, AnchorResistance,
                ref Result
            );
        }
    }

    [TestFixture]
    [Category("Golden")]
    public class GeometricNodeTests {
        private static void RunGeometricNode(in GeometricTestData data, ref NativeList<Point> result) {
            new GeometricNodeJob {
                Anchor = data.Anchor,
                Config = data.Config,
                FixedVelocity = data.FixedVelocity,
                Steering = data.Steering,
                RollSpeed = data.RollSpeed,
                PitchSpeed = data.PitchSpeed,
                YawSpeed = data.YawSpeed,
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
        public void Shuttle_GeometricSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/shuttle.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 0);

            var data = GeometricTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunGeometricNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Shuttle_GeometricSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/shuttle.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 1);

            var data = GeometricTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunGeometricNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Veloci_GeometricSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 0);

            var data = GeometricTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunGeometricNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Veloci_GeometricSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 1);

            var data = GeometricTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunGeometricNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Veloci_GeometricSection3_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 2);

            var data = GeometricTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunGeometricNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Veloci_GeometricSection4_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 3);

            var data = GeometricTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunGeometricNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void AllTypes_GeometricSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 0);

            var data = GeometricTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunGeometricNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void AllTypes_GeometricSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 1);

            var data = GeometricTestBuilder.FromGold(section, Allocator.TempJob);
            var result = new NativeList<Point>(Allocator.TempJob);

            try {
                RunGeometricNode(in data, ref result);
                SimPointComparer.AssertMatchesGold(result, section.outputs.points);
            }
            finally {
                data.Dispose();
                result.Dispose();
            }
        }
    }
}
