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
    public class ShuttleTraversalTests {
        private const string ShuttleKexPath = "Assets/Tests/Assets/shuttle.kex";
        private const float CarSpacing = 3f;
        private const int CarCount = 5;

        [Test]
        public void Shuttle_TraverseFullTrack_CarsRemainRigid() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                var follower = new SimFollower { TraversalIndex = 0, PointIndex = 0f, Facing = 1 };
                const float dt = 0.016f;
                const float rigidityTolerance = 1.5f;

                for (int step = 0; step < 10000; step++) {
                    SimFollowerLogic.Advance(ref follower, in track, dt, Sim.HZ, wrapAtEnd: false, out Point comPoint);
                    if (IsAtEndOfTrack(ref follower, in track)) break;

                    float3 comPosition = comPoint.SpinePosition(comPoint.HeartOffset);
                    float3 comDirection = comPoint.Direction;
                    float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;

                    for (int carIdx = 0; carIdx < CarCount - 1; carIdx++) {
                        float offset1 = carIdx * CarSpacing - halfSpan;
                        float offset2 = (carIdx + 1) * CarSpacing - halfSpan;
                        float3 car1Pos = comPosition + comDirection * offset1 * follower.Facing;
                        float3 car2Pos = comPosition + comDirection * offset2 * follower.Facing;
                        float spacing = math.length(car2Pos - car1Pos);

                        Assert.Less(math.abs(spacing - CarSpacing), rigidityTolerance,
                            $"Rigidity violation at step {step}: spacing={spacing:F2}");
                    }
                }
            });
        }

        [Test]
        public void Shuttle_SplinePositioning_CarsRemainRigidWithOverhang() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                var follower = new SimFollower { TraversalIndex = 0, PointIndex = 0f, Facing = 1 };
                float3[] carPositions = new float3[CarCount];
                const float dt = 0.016f;
                const float splineRigidityTolerance = 3f;

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
                        Assert.Less(math.abs(spacing - CarSpacing), splineRigidityTolerance,
                            $"Spline rigidity violation at step {step}: spacing={spacing:F2}");
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

        [Test]
        public void Shuttle_EndOfTrack_OverhangingCarsOnCorrectSide() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                // Get the last section (REV section after reversal)
                int lastSectionIndex = track.TraversalOrder[track.TraversalCount - 1];
                var lastSection = track.Sections[lastSectionIndex];
                float sectionLength = lastSection.ArcEnd - lastSection.ArcStart;

                // After reversal, follower.Facing = -1
                const int facing = -1;

                // Test at multiple points along the last section
                // For REV section: entry is near ArcEnd (high arc), exit is near ArcStart (low arc)
                float[] testTs = { 0.95f, 0.8f, 0.5f, 0.2f, 0.05f };

                foreach (float t in testTs) {
                    float testArc = lastSection.ArcStart + sectionLength * t;
                    ValidateCarPositionsAtArc(in track, lastSectionIndex, testArc, facing,
                        $"at t={t:F2}");
                }
            });
        }

        private static void ValidateCarPositionsAtArc(in Track track, int sectionIndex, float arc, int facing, string context) {
            track.SamplePoint(sectionIndex, arc, out Point comPoint);

            float3 comPosition = comPoint.SpinePosition(comPoint.HeartOffset);
            float3 trainDirection = comPoint.Direction * facing;
            float3 geoDirection = comPoint.Direction;

            float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;
            float3[] carPositions = new float3[CarCount];

            for (int carIdx = 0; carIdx < CarCount; carIdx++) {
                float offset = carIdx * CarSpacing - halfSpan;
                TrainCarLogic.PositionCarWithOverhang(in track, sectionIndex, comPoint.SpineArc, offset, facing, out SplinePoint carPoint);
                carPositions[carIdx] = carPoint.Position;
            }

            // Validate cars are on correct side of COM
            for (int carIdx = 0; carIdx < CarCount; carIdx++) {
                float offset = carIdx * CarSpacing - halfSpan;
                float3 carToCom = comPosition - carPositions[carIdx];
                float projection = math.dot(carToCom, trainDirection);

                // projection > 0 means COM is ahead of car → car is BEHIND
                bool carIsBehindCom = projection > 0.5f;
                bool shouldBeBehindCom = offset * facing > 0;

                if (math.abs(offset) > 1f) {
                    Assert.AreEqual(shouldBeBehindCom, carIsBehindCom,
                        $"{context}: Car {carIdx} (offset={offset:F1}) on wrong side. " +
                        $"ShouldBeBehind={shouldBeBehindCom}, ActuallyBehind={carIsBehindCom}. " +
                        $"CarPos={carPositions[carIdx]}, COMPos={comPosition}, " +
                        $"trainDir={trainDirection}, geoDir={geoDirection}, projection={projection:F2}");
                }
            }

            // Validate no cars overlap
            for (int i = 0; i < CarCount; i++) {
                for (int j = i + 1; j < CarCount; j++) {
                    float dist = math.distance(carPositions[i], carPositions[j]);
                    Assert.Greater(dist, 0.5f,
                        $"{context}: Cars {i} and {j} overlap! Distance={dist:F2}m");
                }
            }
        }
    }
}
