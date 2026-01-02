using System.IO;
using KexEdit.Document;
using KexEdit.Legacy;
using KexEdit.Sim;
using KexEdit.Spline;
using KexEdit.Trains;
using KexEdit.Trains.Sim;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Track = KexEdit.Track.Track;

namespace Tests.Trains {
    [TestFixture]
    [Category("Unit")]
    public class SwitchTraversalTests {
        private const string SwitchKexPath = "Assets/Tests/Assets/switch.kex";
        private const float CarSpacing = 3f;
        private const int CarCount = 5;

        [Test]
        public void Switch_TraverseFullTrack_PositionsRemainSmooth() {
            WithTrack(SwitchKexPath, (in Track track) => {
                var follower = new SimFollower { TraversalIndex = 0, PointIndex = 0f, Facing = 1 };
                var prevPositions = new float3[CarCount];
                bool hasPrev = false;
                const float dt = 0.016f;
                const float maxPositionDelta = 2f;

                for (int step = 0; step < 10000; step++) {
                    SimFollowerLogic.Advance(ref follower, in track, dt, Sim.HZ, wrapAtEnd: false, out Point comPoint);
                    if (IsAtEndOfTrack(ref follower, in track)) break;

                    float baseArc = comPoint.SpineArc;
                    float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;

                    int sectionIndex = track.TraversalOrder[follower.TraversalIndex];
                    for (int carIdx = 0; carIdx < CarCount; carIdx++) {
                        float offset = carIdx * CarSpacing - halfSpan;
                        TrainCarLogic.PositionCarWithOverhang(in track, sectionIndex, baseArc, offset, follower.Facing, out SplinePoint carPoint);

                        if (hasPrev) {
                            float posDelta = math.length(carPoint.Position - prevPositions[carIdx]);
                            Assert.Less(posDelta, maxPositionDelta,
                                $"Position jump at step {step}, car {carIdx}: delta={posDelta:F2}");
                        }

                        prevPositions[carIdx] = carPoint.Position;
                    }

                    hasPrev = true;
                }
            });
        }

        [Test]
        public void Switch_TraverseFullTrack_DirectionsRemainSmooth() {
            WithTrack(SwitchKexPath, (in Track track) => {
                var follower = new SimFollower { TraversalIndex = 0, PointIndex = 0f, Facing = 1 };
                var prevDirections = new float3[CarCount];
                bool hasPrev = false;
                const float dt = 0.016f;
                const float minDotProduct = 0.9f;

                for (int step = 0; step < 10000; step++) {
                    SimFollowerLogic.Advance(ref follower, in track, dt, Sim.HZ, wrapAtEnd: false, out Point comPoint);
                    if (IsAtEndOfTrack(ref follower, in track)) break;

                    float baseArc = comPoint.SpineArc;
                    float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;

                    int sectionIndex = track.TraversalOrder[follower.TraversalIndex];
                    for (int carIdx = 0; carIdx < CarCount; carIdx++) {
                        float offset = carIdx * CarSpacing - halfSpan;
                        TrainCarLogic.PositionCarWithOverhang(in track, sectionIndex, baseArc, offset, follower.Facing, out SplinePoint carPoint);

                        float3 dir = carPoint.Direction * follower.Facing;

                        if (hasPrev && math.lengthsq(prevDirections[carIdx]) > 0.01f && math.lengthsq(dir) > 0.01f) {
                            float dot = math.dot(math.normalize(dir), math.normalize(prevDirections[carIdx]));
                            Assert.Greater(dot, minDotProduct,
                                $"Direction flip at step {step}, car {carIdx}: dot={dot:F3}");
                        }

                        prevDirections[carIdx] = dir;
                    }

                    hasPrev = true;
                }
            });
        }

        [Test]
        public void Switch_TraverseFullTrack_SpacingRemainsSTable() {
            WithTrack(SwitchKexPath, (in Track track) => {
                var follower = new SimFollower { TraversalIndex = 0, PointIndex = 0f, Facing = 1 };
                var carPositions = new float3[CarCount];
                const float dt = 0.016f;
                const float spacingTolerance = 3f;

                for (int step = 0; step < 10000; step++) {
                    SimFollowerLogic.Advance(ref follower, in track, dt, Sim.HZ, wrapAtEnd: false, out Point comPoint);
                    if (IsAtEndOfTrack(ref follower, in track)) break;

                    float baseArc = comPoint.SpineArc;
                    float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;

                    int sectionIndex = track.TraversalOrder[follower.TraversalIndex];
                    for (int carIdx = 0; carIdx < CarCount; carIdx++) {
                        float offset = carIdx * CarSpacing - halfSpan;
                        TrainCarLogic.PositionCarWithOverhang(in track, sectionIndex, baseArc, offset, follower.Facing, out SplinePoint carPoint);
                        carPositions[carIdx] = carPoint.Position;
                    }

                    for (int carIdx = 0; carIdx < CarCount - 1; carIdx++) {
                        float spacing = math.length(carPositions[carIdx + 1] - carPositions[carIdx]);
                        Assert.Less(math.abs(spacing - CarSpacing), spacingTolerance,
                            $"Spacing violation at step {step}: spacing={spacing:F2}");
                    }
                }
            });
        }

        private delegate void TrackTest(in Track track);

        private static void WithTrack(string path, TrackTest test) {
            Assert.IsTrue(File.Exists(path), $"Test file not found: {path}");
            byte[] kexData = File.ReadAllBytes(path);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);
                try {
                    Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);
                    try { test(in track); }
                    finally { track.Dispose(); }
                }
                finally { coaster.Dispose(); }
            }
            finally { buffer.Dispose(); }
        }

        private static bool IsAtEndOfTrack(ref SimFollower follower, in Track track) {
            if (follower.TraversalIndex >= track.TraversalCount - 1) {
                int sectionIndex = track.TraversalOrder[follower.TraversalIndex];
                var lastSection = track.Sections[sectionIndex];
                if (lastSection.IsValid) {
                    return follower.PointIndex >= lastSection.Length - 1;
                }
            }
            return false;
        }
    }
}
