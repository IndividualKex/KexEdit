using System.IO;
using KexEdit.Document;
using KexEdit.Legacy;
using KexEdit.Spline;
using KexEdit.Trains;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Track = KexEdit.Track.Track;

namespace Tests.Trains {
    /// <summary>
    /// Tests for recursive section link following during car overhang.
    ///
    /// When a car's offset extends past a section boundary into another section,
    /// and that section is shorter than the overhang, the system must recursively
    /// follow links to position the car correctly on the curve.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    public class RecursiveOverhangTests {
        private const string VelociKexPath = "Assets/Tests/Assets/veloci.kex";
        private const float CarSpacing = 3f;
        private const int CarCount = 5;

        /// <summary>
        /// Veloci circuit: Last section (Bridge) links to Section 0 (short curve).
        /// Cars overhanging from Bridge END should follow into Section 0 and beyond,
        /// bending around the curve rather than extrapolating linearly.
        /// </summary>
        [Test]
        public void Veloci_CircuitCompletion_CarsFollowCurveThroughJunction() {
            WithTrack(VelociKexPath, (in Track track) => {
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];
                var lastSection = track.Sections[lastTraversalIdx];

                // Position CoM near the end of the last section
                float baseArc = lastSection.ArcEnd - 1f;
                int facing = lastSection.Facing;

                // Calculate overhang for front car (positive offset from CoM)
                float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;
                float frontCarOffset = halfSpan;

                // This overhang should cross into Section 0 (and possibly beyond)
                TrainCarLogic.PositionCarWithOverhang(
                    in track, lastTraversalIdx, baseArc, frontCarOffset, facing,
                    out SplinePoint frontCar
                );

                // The front car should NOT be at default (which indicates extrapolation fallback)
                Assert.AreNotEqual(float3.zero, frontCar.Position,
                    "Front car position should not be zero (indicates failed link following)");

                // The direction should curve around, not point straight
                // For veloci circuit: Section 0 curves, so direction should differ from last section's end direction
                var lastEndPoint = track.Points[lastSection.EndIndex];
                float dirDot = math.dot(frontCar.Direction, lastEndPoint.Frame.Direction);

                // If recursion works, the car bends around the curve
                // If it extrapolates, direction stays the same (dot ≈ 1.0)
                Assert.Less(dirDot, 0.99f,
                    $"Front car should curve around (dirDot={dirDot:F3}). " +
                    "If dirDot≈1, car is extrapolating linearly instead of following curve.");
            });
        }

        /// <summary>
        /// Veloci circuit: Back cars at the start of the track should follow Prev links
        /// back into Section 8 (Bridge) through the circuit junction.
        /// </summary>
        [Test]
        public void Veloci_CircuitCompletion_BackCarsFollowPrevThroughJunction() {
            WithTrack(VelociKexPath, (in Track track) => {
                int firstTraversalIdx = track.TraversalOrder[0];
                var firstSection = track.Sections[firstTraversalIdx];

                // Position CoM near the start of the first section
                float baseArc = firstSection.ArcStart + 1f;
                int facing = firstSection.Facing;

                // Calculate overhang for back car (negative offset from CoM)
                float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;
                float backCarOffset = -halfSpan;

                TrainCarLogic.PositionCarWithOverhang(
                    in track, firstTraversalIdx, baseArc, backCarOffset, facing,
                    out SplinePoint backCar
                );

                Assert.AreNotEqual(float3.zero, backCar.Position,
                    "Back car position should not be zero (indicates failed link following)");
            });
        }

        /// <summary>
        /// Test that extremely short circular tracks don't cause infinite recursion.
        /// When maxDepth is exceeded, should gracefully fall back to extrapolation.
        /// </summary>
        [Test]
        public void TinyCircuit_DoesNotCrash() {
            // Use veloci as proxy - even if track is short, shouldn't crash
            WithTrack(VelociKexPath, (in Track track) => {
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];
                var lastSection = track.Sections[lastTraversalIdx];

                float baseArc = lastSection.ArcEnd - 0.5f;
                int facing = lastSection.Facing;

                // Extreme overhang that would loop the entire track multiple times
                float extremeOverhang = 1000f;

                // Should not throw, should not hang - just call directly
                TrainCarLogic.PositionCarWithOverhang(
                    in track, lastTraversalIdx, baseArc, extremeOverhang, facing,
                    out SplinePoint result
                );

                // If we got here without crashing/hanging, test passes
                // Result will likely be default or extrapolated, which is fine
                Assert.Pass("Extreme overhang did not crash or hang");
            });
        }

        /// <summary>
        /// Verify car spacing rigidity through the circuit junction.
        /// All cars should maintain consistent spacing when crossing section boundaries.
        /// </summary>
        [Test]
        public void Veloci_CircuitJunction_CarSpacingIsRigid() {
            WithTrack(VelociKexPath, (in Track track) => {
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];
                var lastSection = track.Sections[lastTraversalIdx];

                // Position CoM so some cars are before junction, some after
                float baseArc = lastSection.ArcEnd - 2f;
                int facing = lastSection.Facing;

                float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;
                var carPositions = new float3[CarCount];

                for (int i = 0; i < CarCount; i++) {
                    float offset = i * CarSpacing - halfSpan;
                    TrainCarLogic.PositionCarWithOverhang(
                        in track, lastTraversalIdx, baseArc, offset, facing,
                        out SplinePoint carPoint
                    );
                    carPositions[i] = carPoint.Position;
                }

                // Check spacing between consecutive cars
                for (int i = 1; i < CarCount; i++) {
                    float actualSpacing = math.distance(carPositions[i], carPositions[i - 1]);
                    float expectedSpacing = CarSpacing;
                    float tolerance = 0.5f; // Allow some variance due to curvature

                    Assert.That(actualSpacing, Is.InRange(expectedSpacing - tolerance, expectedSpacing + tolerance),
                        $"Car {i} spacing deviation: expected ~{expectedSpacing:F2}m, got {actualSpacing:F2}m");
                }
            });
        }

        #region Helper Methods

        private delegate void TrackTest(in Track track);

        private static void WithTrack(string path, TrackTest test) {
            Assert.IsTrue(File.Exists(path), $"Test file not found: {path}");
            byte[] kexData = File.ReadAllBytes(path);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);
                try {
                    Track.Build(in coaster, Allocator.TempJob, out var track);
                    try { test(in track); }
                    finally { track.Dispose(); }
                }
                finally { coaster.Dispose(); }
            }
            finally { buffer.Dispose(); }
        }

        #endregion
    }
}
