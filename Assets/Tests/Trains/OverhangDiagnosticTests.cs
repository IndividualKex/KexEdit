using System.IO;
using KexEdit.Document;
using KexEdit.Legacy;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Track = KexEdit.Track.Track;

namespace Tests.Trains {
    [TestFixture]
    [Category("Unit")]
    public class OverhangDiagnosticTests {
        private const string SwitchKexPath = "Assets/Tests/Assets/switch.kex";

        private static bool IsCosmetic(in Track track, int sectionIndex) {
            for (int i = 0; i < track.TraversalCount; i++) {
                if (track.TraversalOrder[i] == sectionIndex) return false;
            }
            return true;
        }

        private static string FormatLink(KexEdit.Track.SectionLink link) {
            if (!link.IsValid) return "-1";
            return $"{link.Index}(AtStart={link.AtStart}, Flip={link.Flip})";
        }

        [Test]
        public void Diagnostic_SwitchSectionConnections() {
            WithTrack(SwitchKexPath, (in Track track) => {
                UnityEngine.Debug.Log($"=== SWITCH SECTION CONNECTIONS ===");
                UnityEngine.Debug.Log($"TraversalCount: {track.TraversalCount}, SectionCount: {track.SectionCount}");

                for (int i = 0; i < track.TraversalCount; i++) {
                    int sectionIdx = track.TraversalOrder[i];
                    var section = track.Sections[sectionIdx];
                    var startPt = track.Points[section.StartIndex];
                    var endPt = track.Points[section.EndIndex];

                    string facing = section.Facing == 1 ? "FWD" : "REV";
                    UnityEngine.Debug.Log($"\nTraversal[{i}] = Section {sectionIdx} ({facing}):");
                    UnityEngine.Debug.Log($"  StartPos: {startPt.HeartPosition}, StartDir: {startPt.Frame.Direction}");
                    UnityEngine.Debug.Log($"  EndPos: {endPt.HeartPosition}, EndDir: {endPt.Frame.Direction}");
                    UnityEngine.Debug.Log($"  Prev={FormatLink(section.Prev)}, Next={FormatLink(section.Next)}");

                    if (section.Prev.IsValid) {
                        var prev = track.Sections[section.Prev.Index];
                        var prevStart = track.Points[prev.StartIndex];
                        var prevEnd = track.Points[prev.EndIndex];
                        float distStartStart = math.distance(startPt.HeartPosition, prevStart.HeartPosition);
                        float distStartEnd = math.distance(startPt.HeartPosition, prevEnd.HeartPosition);
                        string prevFacing = prev.Facing == 1 ? "FWD" : "REV";
                        bool isCosmetic = IsCosmetic(in track, section.Prev.Index);
                        UnityEngine.Debug.Log($"  -> Prev section {section.Prev.Index} ({prevFacing}{(isCosmetic ? ", COSMETIC" : "")}): " +
                            $"distToStart={distStartStart:F3}, distToEnd={distStartEnd:F3}");
                    }

                    if (section.Next.IsValid) {
                        var next = track.Sections[section.Next.Index];
                        var nextStart = track.Points[next.StartIndex];
                        var nextEnd = track.Points[next.EndIndex];
                        float distEndStart = math.distance(endPt.HeartPosition, nextStart.HeartPosition);
                        float distEndEnd = math.distance(endPt.HeartPosition, nextEnd.HeartPosition);
                        string nextFacing = next.Facing == 1 ? "FWD" : "REV";
                        bool isCosmetic = IsCosmetic(in track, section.Next.Index);
                        UnityEngine.Debug.Log($"  -> Next section {section.Next.Index} ({nextFacing}{(isCosmetic ? ", COSMETIC" : "")}): " +
                            $"distToStart={distEndStart:F3}, distToEnd={distEndEnd:F3}");
                    }
                }

                // Find cosmetic spike
                UnityEngine.Debug.Log($"\n=== COSMETIC SECTIONS ===");
                for (int i = 0; i < track.SectionCount; i++) {
                    if (!track.Sections[i].IsValid) continue;
                    if (!IsCosmetic(in track, i)) continue;

                    var section = track.Sections[i];
                    var startPt = track.Points[section.StartIndex];
                    var endPt = track.Points[section.EndIndex];
                    string facing = section.Facing == 1 ? "FWD" : "REV";

                    UnityEngine.Debug.Log($"Cosmetic section {i} ({facing}):");
                    UnityEngine.Debug.Log($"  StartPos: {startPt.HeartPosition}, StartDir: {startPt.Frame.Direction}");
                    UnityEngine.Debug.Log($"  EndPos: {endPt.HeartPosition}, EndDir: {endPt.Frame.Direction}");
                    UnityEngine.Debug.Log($"  Prev={FormatLink(section.Prev)}, Next={FormatLink(section.Next)}");
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
    }
}
