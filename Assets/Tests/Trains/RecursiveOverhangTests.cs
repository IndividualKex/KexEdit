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
        private const string CircuitKexPath = "Assets/Tests/Assets/circuit.kex";
        private const float CarSpacing = 3f;
        private const int CarCount = 5;

        /// <summary>
        /// Circuit track: Test that front cars with overhang are positioned correctly
        /// when crossing the circuit junction.
        /// </summary>
        [Test]
        public void Circuit_CircuitCompletion_FrontCarsPositionedCorrectly() {
            WithTrack(CircuitKexPath, (in Track track) => {
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

                // Direction should be normalized (valid frame)
                float dirLength = math.length(frontCar.Direction);
                Assert.That(dirLength, Is.InRange(0.99f, 1.01f),
                    $"Front car direction should be normalized, got length {dirLength:F4}");
            });
        }

        /// <summary>
        /// Circuit track: Back cars at the start of the track should follow Prev links
        /// back into Section 8 (Bridge) through the circuit junction.
        /// </summary>
        [Test]
        public void Circuit_CircuitCompletion_BackCarsFollowPrevThroughJunction() {
            WithTrack(CircuitKexPath, (in Track track) => {
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
            // Use circuit as proxy - even if track is short, shouldn't crash
            WithTrack(CircuitKexPath, (in Track track) => {
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
        public void Circuit_CircuitJunction_CarSpacingIsRigid() {
            WithTrack(CircuitKexPath, (in Track track) => {
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
                    Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);
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
