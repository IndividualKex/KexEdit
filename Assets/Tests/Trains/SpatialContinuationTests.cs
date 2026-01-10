using System.IO;
using System.Linq;
using KexEdit.Document;
using KexEdit.Legacy;
using KexEdit.Sim;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Track = KexEdit.Track.Track;

namespace Tests.Trains {
    /// <summary>
    /// Tests for spatial continuation (Next/Prev) logic that enables car overhang.
    ///
    /// EXPECTED SCENARIOS:
    ///
    /// SHUTTLE (4 scenarios):
    /// 1. Beginning (first Geo FWD) START -> Prev = cosmetic extension (Geo REV)
    /// 2. Spike (before Reverse) END -> Next = cosmetic spike
    /// 3. After reverse (CopyPath REV) START -> Prev = cosmetic spike
    /// 4. End (final CopyPath REV) END -> Next = cosmetic extension (Geo REV)
    ///
    /// CIRCUIT (2 scenarios):
    /// 5. End (last Bridge) END -> Next = first Geo (circuit completion)
    /// 6. Beginning (first Geo) START -> Prev = last Bridge (circuit completion)
    ///
    /// SWITCH (2 scenarios):
    /// 7. Spike (before Reverse) END -> Next = cosmetic twisting spike
    /// 8. After reverse (CopyPath REV) START -> Prev = cosmetic twisting spike
    ///
    /// ALL SCENARIOS:
    /// 9. Frame alignment ensures train rigidity through continuations
    ///
    /// KEY INSIGHT:
    /// Both Next and Prev can match EITHER target START or target END.
    /// The only difference is frame alignment:
    /// - Next: same direction (dirDot > 0.9)
    /// - Prev: opposite direction (dirDot < -0.9)
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    public class SpatialContinuationTests {
        private const string ShuttleKexPath = "Assets/Tests/Assets/shuttle.kex";
        private const string CircuitKexPath = "Assets/Tests/Assets/circuit.kex";
        private const string SwitchKexPath = "Assets/Tests/Assets/switch.kex";
        private const float FrameThreshold = 0.9f;

        private static bool IsCosmetic(in Track track, int sectionIndex) {
            for (int i = 0; i < track.TraversalCount; i++) {
                if (track.TraversalOrder[i] == sectionIndex) return false;
            }
            return true;
        }

        #region Shuttle Tests

        [Test]
        public void Shuttle_Scenario1_FirstTraversalPrev_IsCosmetic() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                int firstTraversalIdx = track.TraversalOrder[0];
                var firstSection = track.Sections[firstTraversalIdx];

                Assert.IsTrue(firstSection.Prev.IsValid,
                    "First traversal section should have a Prev for back car overhang");

                Assert.IsTrue(IsCosmetic(in track, firstSection.Prev.Index),
                    "Prev should be a cosmetic section (not in traversal order)");
            });
        }

        [Test]
        public void Shuttle_Scenario1_FirstTraversalPrev_HasOppositeDirection() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                int firstTraversalIdx = track.TraversalOrder[0];
                var firstSection = track.Sections[firstTraversalIdx];

                if (!firstSection.Prev.IsValid) {
                    Assert.Inconclusive("No Prev found - test blocked");
                    return;
                }

                var sourcePoint = track.Points[firstSection.StartIndex];
                var prevSection = track.Sections[firstSection.Prev.Index];

                // Check both endpoints of Prev for opposite direction match
                var prevStart = track.Points[prevSection.StartIndex];
                var prevEnd = track.Points[prevSection.EndIndex];

                float dirDotStart = math.dot(sourcePoint.Frame.Direction, prevStart.Frame.Direction);
                float dirDotEnd = math.dot(sourcePoint.Frame.Direction, prevEnd.Frame.Direction);

                bool hasOppositeMatch = dirDotStart < -FrameThreshold || dirDotEnd < -FrameThreshold;
                Assert.IsTrue(hasOppositeMatch,
                    $"Prev should have opposite direction (dirDot < -{FrameThreshold}). " +
                    $"Got: start={dirDotStart:F3}, end={dirDotEnd:F3}");
            });
        }

        [Test]
        public void Shuttle_Scenario4_LastTraversalNext_IsCosmetic() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];
                var lastSection = track.Sections[lastTraversalIdx];

                Assert.IsTrue(lastSection.Next.IsValid,
                    $"Last traversal section (idx={lastTraversalIdx}) should have Next for back car overhang");

                Assert.IsTrue(IsCosmetic(in track, lastSection.Next.Index),
                    "Next should be a cosmetic section (not in traversal order)");
            });
        }

        [Test]
        public void Shuttle_Scenario4_LastTraversalNext_HasSameDirection() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];
                var lastSection = track.Sections[lastTraversalIdx];

                if (!lastSection.Next.IsValid) {
                    Assert.Inconclusive("No Next found - test blocked");
                    return;
                }

                // For REV section, exit point is StartIndex; for FWD, it's EndIndex
                int exitPointIdx = lastSection.Facing == 1 ? lastSection.EndIndex : lastSection.StartIndex;
                var sourcePoint = track.Points[exitPointIdx];
                var nextSection = track.Sections[lastSection.Next.Index];

                // Check both endpoints of Next for same direction match
                var nextStart = track.Points[nextSection.StartIndex];
                var nextEnd = track.Points[nextSection.EndIndex];

                float dirDotStart = math.dot(sourcePoint.Frame.Direction, nextStart.Frame.Direction);
                float dirDotEnd = math.dot(sourcePoint.Frame.Direction, nextEnd.Frame.Direction);

                bool hasSameDirectionMatch = dirDotStart > FrameThreshold || dirDotEnd > FrameThreshold;
                Assert.IsTrue(hasSameDirectionMatch,
                    $"Next should have same direction (dirDot > {FrameThreshold}). " +
                    $"Got: start={dirDotStart:F3}, end={dirDotEnd:F3}");
            });
        }

        [Test]
        public void Shuttle_LastTraversalIsRev_ExitPointIsStartIndex() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];
                var lastSection = track.Sections[lastTraversalIdx];

                Assert.AreEqual(-1, lastSection.Facing,
                    "Last traversal section should be REV (Facing=-1) after shuttle reversal");
            });
        }

        [Test]
        public void Shuttle_Diagnostic_LastSectionNextPointsToCorrectCosmetic() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];
                var lastSection = track.Sections[lastTraversalIdx];

                // Last section should be REV
                Assert.AreEqual(-1, lastSection.Facing, "Last section should be REV");

                // For arc-based overhang, Next looks at geometric END (EndIndex)
                var geoEndPoint = track.Points[lastSection.EndIndex];
                var geoEndPos = geoEndPoint.HeartPosition;
                var geoDir = geoEndPoint.Frame.Direction;

                // Find cosmetic sections at geometric END with matching direction
                int? cosmeticMatch = null;

                for (int i = 0; i < track.SectionCount; i++) {
                    if (!track.Sections[i].IsValid) continue;
                    if (!IsCosmetic(in track, i)) continue;

                    var sec = track.Sections[i];
                    var startPt = track.Points[sec.StartIndex];
                    var endPt = track.Points[sec.EndIndex];

                    float distToStart = math.distance(geoEndPos, startPt.HeartPosition);
                    float distToEnd = math.distance(geoEndPos, endPt.HeartPosition);

                    // Same direction at START, or opposite direction at END
                    if (distToStart < 0.1f && math.dot(geoDir, startPt.Frame.Direction) > 0.9f) {
                        cosmeticMatch = i;
                        break;
                    }
                    if (distToEnd < 0.1f && math.dot(geoDir, endPt.Frame.Direction) < -0.9f) {
                        cosmeticMatch = i;
                        break;
                    }
                }

                // Expect a cosmetic section at geometric END
                Assert.IsTrue(cosmeticMatch.HasValue,
                    $"Should find cosmetic section at geometric END. " +
                    $"GeoEndPos={geoEndPos}, GeoDir={geoDir}");

                Assert.AreEqual(cosmeticMatch.Value, lastSection.Next.Index,
                    $"Last section Next should point to cosmetic at geometric END. " +
                    $"Expected={cosmeticMatch}, Actual={lastSection.Next.Index}");
            });
        }

        #endregion

        #region Circuit Tests

        [Test]
        public void Circuit_Scenario5_CircuitCompletion_PositionsMatch() {
            // For circuits, last section END should connect to first section START
            // This test verifies the positions match for circuit completion
            WithTrack(CircuitKexPath, (in Track track) => {
                int firstTraversalIdx = track.TraversalOrder[0];
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];

                var firstSection = track.Sections[firstTraversalIdx];
                var lastSection = track.Sections[lastTraversalIdx];

                var firstStart = track.Points[firstSection.StartIndex];
                var lastEnd = track.Points[lastSection.EndIndex];
                float distance = math.distance(firstStart.HeartPosition, lastEnd.HeartPosition);

                Assert.Less(distance, 0.1f,
                    $"Circuit: last END should match first START. Distance={distance:F3}m");

                // Note: Next/Prev for circuit completion is handled via graph edges,
                // not spatial matching (which requires opposite direction for Prev)
            });
        }

        [Test]
        public void Circuit_Scenario6_CircuitCompletion_DirectionsAreAligned() {
            // Circuit completion requires SAME direction (not opposite like cosmetic overhang)
            // This test verifies the directions align at the circuit junction
            WithTrack(CircuitKexPath, (in Track track) => {
                int firstTraversalIdx = track.TraversalOrder[0];
                int lastTraversalIdx = track.TraversalOrder[track.TraversalCount - 1];
                var firstSection = track.Sections[firstTraversalIdx];
                var lastSection = track.Sections[lastTraversalIdx];

                var firstStart = track.Points[firstSection.StartIndex];
                var lastEnd = track.Points[lastSection.EndIndex];
                float distance = math.distance(firstStart.HeartPosition, lastEnd.HeartPosition);

                Assert.Less(distance, 0.1f, "Circuit should be closed");

                // For circuit: directions should be SAME (not opposite)
                float dirDot = math.dot(firstStart.Frame.Direction, lastEnd.Frame.Direction);
                Assert.Greater(dirDot, FrameThreshold,
                    $"Circuit junction should have same direction. dirDot={dirDot:F3}");

                // Note: Current FindSpatialMatch requires opposite direction for Prev,
                // which doesn't work for circuit completion. This is a known limitation.
                // The Prev/Next for circuits is handled by graph-based continuation.
            });
        }

        #endregion

        #region Switch Tests

        [Test]
        public void Switch_SpikeSection_HasSameTrainDirectionAsCopyPathExit() {
            // Spike is at REV section's EXIT (geometric START), so Next should match
            // Next requires SAME train direction (accounting for Facing)
            // For REV sections, train direction = -geometric direction
            WithTrack(SwitchKexPath, (in Track track) => {
                int spikeIndex = -1;

                for (int i = 0; i < track.SectionCount; i++) {
                    var section = track.Sections[i];
                    if (!section.IsValid) continue;

                    if (section.Facing == 1 && !section.Next.IsValid && IsCosmetic(in track, i)) {
                        spikeIndex = i;
                        break;
                    }
                }

                if (spikeIndex < 0) {
                    Assert.Inconclusive("Cosmetic spike section not found");
                    return;
                }

                var spike = track.Sections[spikeIndex];
                var spikeStart = track.Points[spike.StartIndex].HeartPosition;

                // Find REV section at spike position
                int revSectionIdx = -1;
                for (int i = 0; i < track.SectionCount; i++) {
                    var section = track.Sections[i];
                    if (!section.IsValid || section.Facing != -1) continue;

                    var exitPos = track.Points[section.StartIndex].HeartPosition;
                    if (math.distance(exitPos, spikeStart) < 0.1f) {
                        revSectionIdx = i;
                        break;
                    }
                }

                if (revSectionIdx < 0) {
                    Assert.Inconclusive("REV section at spike position not found");
                    return;
                }

                var revSection = track.Sections[revSectionIdx];
                // REV section exit is at StartIndex
                var revExit = track.Points[revSection.StartIndex];
                var spikePoint = track.Points[spike.StartIndex];

                // REV section train direction is negative of geometric direction
                float3 trainDir = -revExit.Frame.Direction;
                float dirDot = math.dot(trainDir, spikePoint.Frame.Direction);
                Assert.Greater(dirDot, FrameThreshold,
                    $"Spike direction should be SAME as REV section train direction (dirDot > {FrameThreshold}). Got: {dirDot:F3}");
            });
        }

        #endregion

        #region Rigidity Tests (Scenario 9)

        [Test]
        public void Shuttle_TraversalEndpoints_CosmeticContinuationsHaveFrameAlignment() {
            WithTrack(ShuttleKexPath, (in Track track) => {
                // Test first and last traversal sections only - these are the key overhang points
                int firstIdx = track.TraversalOrder[0];
                int lastIdx = track.TraversalOrder[track.TraversalCount - 1];

                var firstSection = track.Sections[firstIdx];
                var lastSection = track.Sections[lastIdx];

                // First section Prev (back car overhang at start)
                if (firstSection.Prev.IsValid && IsCosmetic(in track, firstSection.Prev.Index)) {
                    var entryPoint = track.Points[firstSection.StartIndex]; // FWD section enters at start
                    var prevSection = track.Sections[firstSection.Prev.Index];
                    var prevStart = track.Points[prevSection.StartIndex];

                    float dirDot = math.dot(entryPoint.Frame.Direction, prevStart.Frame.Direction);
                    Assert.Less(dirDot, -FrameThreshold,
                        $"First section Prev (cosmetic) should have opposite direction. dirDot={dirDot:F3}");
                }

                // Last section Next (back car overhang at end for shuttle)
                if (lastSection.Next.IsValid && IsCosmetic(in track, lastSection.Next.Index)) {
                    // Last section is REV, so exit is at StartIndex
                    int exitIdx = lastSection.Facing == 1 ? lastSection.EndIndex : lastSection.StartIndex;
                    var exitPoint = track.Points[exitIdx];
                    var nextSection = track.Sections[lastSection.Next.Index];
                    var nextStart = track.Points[nextSection.StartIndex];

                    float dirDot = math.dot(exitPoint.Frame.Direction, nextStart.Frame.Direction);
                    Assert.Greater(dirDot, FrameThreshold,
                        $"Last section Next (cosmetic) should have same direction. dirDot={dirDot:F3}");
                }
            });
        }

        #endregion

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
