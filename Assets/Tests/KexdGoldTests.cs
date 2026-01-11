using System.IO;
using KexEdit.Persistence;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Coaster = KexEdit.Document.Document;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class KexdGoldTests {
        [Test]
        public void CircuitKexd_CSharp_MatchesGoldData() {
            RunCSharpParityTest("circuit_kexd", "circuit");
        }

        [Test]
        public void SwitchKexd_CSharp_MatchesGoldData() {
            RunCSharpSwitchParityTest("switch_kexd", "switch");
        }

        [Test]
        public void AllTypesKexd_CSharp_MatchesGoldData() {
            RunCSharpParityTest("all_types_kexd", "all_types");
        }

        [Test]
        public void ShuttleKexd_CSharp_MatchesGoldData() {
            RunCSharpParityTest("shuttle_kexd", "shuttle");
        }

        [Test]
        public void CircuitKexd_Rust_MatchesGoldData() {
            RunRustParityTest("circuit_kexd", "circuit");
        }

        [Test]
        public void SwitchKexd_Rust_MatchesGoldData() {
            RunRustSwitchParityTest("switch_kexd", "switch");
        }

        [Test]
        public void AllTypesKexd_Rust_MatchesGoldData() {
            RunRustParityTest("all_types_kexd", "all_types");
        }

        [Test]
        public void ShuttleKexd_Rust_MatchesGoldData() {
            RunRustParityTest("shuttle_kexd", "shuttle");
        }

        [Test]
        public void CircuitKexd_CSharp_RoundTrip_PreservesData() {
            RunCSharpRoundTripTest("circuit_kexd");
        }

        [Test]
        public void SwitchKexd_CSharp_RoundTrip_PreservesData() {
            RunCSharpRoundTripTest("switch_kexd");
        }

        [Test]
        public void AllTypesKexd_CSharp_RoundTrip_PreservesData() {
            RunCSharpRoundTripTest("all_types_kexd");
        }

        [Test]
        public void ShuttleKexd_CSharp_RoundTrip_PreservesData() {
            RunCSharpRoundTripTest("shuttle_kexd");
        }

        [Test]
        public void CircuitKexd_Rust_RoundTrip_PreservesData() {
            RunRustRoundTripTest("circuit_kexd");
        }

        [Test]
        public void SwitchKexd_Rust_RoundTrip_PreservesData() {
            RunRustRoundTripTest("switch_kexd");
        }

        [Test]
        public void AllTypesKexd_Rust_RoundTrip_PreservesData() {
            RunRustRoundTripTest("all_types_kexd");
        }

        [Test]
        public void ShuttleKexd_Rust_RoundTrip_PreservesData() {
            RunRustRoundTripTest("shuttle_kexd");
        }

        private static void RunCSharpParityTest(string kexdName, string goldName) {
            var gold = GoldDataLoader.Load($"Assets/Tests/TrackData/{goldName}.json");
            var kexdPath = $"Assets/Tests/Assets/{kexdName}.kex";

            Assert.IsTrue(File.Exists(kexdPath), $"Test file not found: {kexdPath}");

            byte[] kexdData = File.ReadAllBytes(kexdPath);
            var buffer = new NativeArray<byte>(kexdData, Allocator.Temp);

            try {
                var reader = new ChunkReader(buffer);
                var coaster = CoasterSerializer.Read(ref reader, Allocator.TempJob);

                try {
                    KexEdit.Track.Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

                    try {
                        Assert.Greater(track.SectionCount, 0, "No sections generated");
                        AssertAllSectionsMatch(gold, track);
                    }
                    finally {
                        track.Dispose();
                    }
                }
                finally {
                    coaster.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        private static void RunCSharpSwitchParityTest(string kexdName, string goldName) {
            var gold = GoldDataLoader.Load($"Assets/Tests/TrackData/{goldName}.json");
            var kexdPath = $"Assets/Tests/Assets/{kexdName}.kex";

            Assert.IsTrue(File.Exists(kexdPath), $"Test file not found: {kexdPath}");

            byte[] kexdData = File.ReadAllBytes(kexdPath);
            var buffer = new NativeArray<byte>(kexdData, Allocator.Temp);

            try {
                var reader = new ChunkReader(buffer);
                var coaster = CoasterSerializer.Read(ref reader, Allocator.TempJob);

                try {
                    KexEdit.Track.Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

                    try {
                        Assert.Greater(track.SectionCount, 0, "No sections generated");
                        AssertSwitchEndpointsMatch(gold, track);
                    }
                    finally {
                        track.Dispose();
                    }
                }
                finally {
                    coaster.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        private static void RunRustParityTest(string kexdName, string goldName) {
            var gold = GoldDataLoader.Load($"Assets/Tests/TrackData/{goldName}.json");
            var kexdPath = $"Assets/Tests/Assets/{kexdName}.kex";

            Assert.IsTrue(File.Exists(kexdPath), $"Test file not found: {kexdPath}");

            byte[] kexdData = File.ReadAllBytes(kexdPath);
            var buffer = new NativeArray<byte>(kexdData, Allocator.Temp);

            try {
                var coaster = KexEnginePersistence.Deserialize(buffer, Allocator.TempJob);

                try {
                    KexEdit.Track.Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

                    try {
                        Assert.Greater(track.SectionCount, 0, "No sections generated");
                        AssertAllSectionsMatch(gold, track);
                    }
                    finally {
                        track.Dispose();
                    }
                }
                finally {
                    coaster.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        private static void RunRustSwitchParityTest(string kexdName, string goldName) {
            var gold = GoldDataLoader.Load($"Assets/Tests/TrackData/{goldName}.json");
            var kexdPath = $"Assets/Tests/Assets/{kexdName}.kex";

            Assert.IsTrue(File.Exists(kexdPath), $"Test file not found: {kexdPath}");

            byte[] kexdData = File.ReadAllBytes(kexdPath);
            var buffer = new NativeArray<byte>(kexdData, Allocator.Temp);

            try {
                var coaster = KexEnginePersistence.Deserialize(buffer, Allocator.TempJob);

                try {
                    KexEdit.Track.Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

                    try {
                        Assert.Greater(track.SectionCount, 0, "No sections generated");
                        AssertSwitchEndpointsMatch(gold, track);
                    }
                    finally {
                        track.Dispose();
                    }
                }
                finally {
                    coaster.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        private static void RunCSharpRoundTripTest(string kexdName) {
            var kexdPath = $"Assets/Tests/Assets/{kexdName}.kex";

            Assert.IsTrue(File.Exists(kexdPath), $"Test file not found: {kexdPath}");

            byte[] kexdData = File.ReadAllBytes(kexdPath);
            var buffer = new NativeArray<byte>(kexdData, Allocator.Temp);

            try {
                var reader = new ChunkReader(buffer);
                var original = CoasterSerializer.Read(ref reader, Allocator.TempJob);

                try {
                    using var writer = new ChunkWriter(Allocator.Temp);
                    CoasterSerializer.Write(writer, in original);
                    var serializedData = writer.ToArray();

                    var reader2 = new ChunkReader(serializedData);
                    var restored = CoasterSerializer.Read(ref reader2, Allocator.TempJob);

                    try {
                        AssertCoastersEqual(original, restored, kexdName);
                    }
                    finally {
                        restored.Dispose();
                    }
                }
                finally {
                    original.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        private static void RunRustRoundTripTest(string kexdName) {
            var kexdPath = $"Assets/Tests/Assets/{kexdName}.kex";

            Assert.IsTrue(File.Exists(kexdPath), $"Test file not found: {kexdPath}");

            byte[] kexdData = File.ReadAllBytes(kexdPath);
            var buffer = new NativeArray<byte>(kexdData, Allocator.Temp);

            try {
                var original = KexEnginePersistence.Deserialize(buffer, Allocator.TempJob);

                try {
                    var serializedData = KexEnginePersistence.Serialize(in original, Allocator.Temp);

                    try {
                        var restored = KexEnginePersistence.Deserialize(serializedData, Allocator.TempJob);

                        try {
                            AssertCoastersEqual(original, restored, kexdName);
                        }
                        finally {
                            restored.Dispose();
                        }
                    }
                    finally {
                        serializedData.Dispose();
                    }
                }
                finally {
                    original.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        private static void AssertCoastersEqual(in Coaster expected, in Coaster actual, string context) {
            Assert.AreEqual(expected.Graph.NodeIds.Length, actual.Graph.NodeIds.Length, $"{context}: Node count mismatch");
            Assert.AreEqual(expected.Graph.EdgeIds.Length, actual.Graph.EdgeIds.Length, $"{context}: Edge count mismatch");
            Assert.AreEqual(expected.Graph.PortIds.Length, actual.Graph.PortIds.Length, $"{context}: Port count mismatch");

            for (int i = 0; i < expected.Graph.NodeIds.Length; i++) {
                Assert.AreEqual(expected.Graph.NodeIds[i], actual.Graph.NodeIds[i], $"{context}: Node ID mismatch at index {i}");
                Assert.AreEqual(expected.Graph.NodeTypes[i], actual.Graph.NodeTypes[i], $"{context}: Node type mismatch at index {i}");
            }

            Assert.AreEqual(expected.Scalars.Count, actual.Scalars.Count, $"{context}: Scalar count mismatch");
            foreach (var kv in expected.Scalars) {
                Assert.IsTrue(actual.Scalars.TryGetValue(kv.Key, out float actualValue), $"{context}: Scalar key {kv.Key} not found");
                Assert.AreEqual(kv.Value, actualValue, 0.0001f, $"{context}: Scalar value mismatch for key {kv.Key}");
            }

            Assert.AreEqual(expected.Vectors.Count, actual.Vectors.Count, $"{context}: Vector count mismatch");
            foreach (var kv in expected.Vectors) {
                Assert.IsTrue(actual.Vectors.TryGetValue(kv.Key, out var actualValue), $"{context}: Vector key {kv.Key} not found");
                Assert.AreEqual(kv.Value.x, actualValue.x, 0.0001f, $"{context}: Vector.x mismatch for key {kv.Key}");
                Assert.AreEqual(kv.Value.y, actualValue.y, 0.0001f, $"{context}: Vector.y mismatch for key {kv.Key}");
                Assert.AreEqual(kv.Value.z, actualValue.z, 0.0001f, $"{context}: Vector.z mismatch for key {kv.Key}");
            }

            Assert.AreEqual(expected.Flags.Count, actual.Flags.Count, $"{context}: Flags count mismatch");
            foreach (var kv in expected.Flags) {
                Assert.IsTrue(actual.Flags.TryGetValue(kv.Key, out var actualValue), $"{context}: Flag key {kv.Key} not found");
                Assert.AreEqual(kv.Value, actualValue, $"{context}: Flag value mismatch for key {kv.Key}");
            }

            Assert.AreEqual(expected.Keyframes.Ranges.Count, actual.Keyframes.Ranges.Count, $"{context}: Keyframe count mismatch");
        }

        private static void AssertAllSectionsMatch(GoldTrackData gold, KexEdit.Track.Track track) {
            int sectionsChecked = 0;
            int cumulativePoints = 0;
            bool afterBridge = false;

            foreach (var section in gold.sections) {
                int sectionPoints = section.outputs?.points?.Count ?? 0;
                uint nodeId = section.nodeId;
                bool isBridge = section.nodeType == "Bridge";

                if (isBridge) {
                    Debug.Log($"Skipping Bridge nodeId={nodeId}: legacy .kex format doesn't store target anchor data");
                    cumulativePoints += sectionPoints;
                    afterBridge = true;
                    continue;
                }

                if (sectionPoints == 0) {
                    continue;
                }

                bool isCopyPath = section.nodeType == "CopyPathSection";

                Assert.IsTrue(track.NodeToSection.TryGetValue(nodeId, out int sectionIndex),
                    $"No section found for nodeId {nodeId} ({section.nodeType})");

                var trackSection = track.Sections[sectionIndex];
                var sectionPointsArray = track.Points.AsArray().GetSubArray(trackSection.StartIndex, trackSection.Length);

                var goldDur = section.inputs?.duration;
                Debug.Log($"Checking {section.nodeType} nodeId={nodeId}: {sectionPointsArray.Length} points (gold expects {sectionPoints}, gold duration={goldDur?.value} {goldDur?.type}, cumulative offset={cumulativePoints}, afterBridge={afterBridge})");

                const int HIGH_OFFSET_THRESHOLD = 5000;

                if (isCopyPath || afterBridge) {
                    AssertEndpointsMatch(sectionPointsArray, section.outputs.points, nodeId, section.nodeType, 5e-2f);
                    cumulativePoints += sectionPoints;
                }
                else if (cumulativePoints >= HIGH_OFFSET_THRESHOLD) {
                    AssertEndpointsMatch(sectionPointsArray, section.outputs.points, nodeId, section.nodeType, 5.0f);
                    cumulativePoints += sectionPoints;
                }
                else {
                    SimPointComparer.AssertMatchesGold(sectionPointsArray, section.outputs.points, cumulativePoints);
                    cumulativePoints += sectionPoints;
                }
                sectionsChecked++;
            }

            Assert.Greater(sectionsChecked, 0, "No sections with points found in gold data");
            Debug.Log($"Verified {sectionsChecked} sections (total {cumulativePoints} cumulative points)");
        }

        private static void AssertSwitchEndpointsMatch(GoldTrackData gold, KexEdit.Track.Track track) {
            bool afterCopyPath = false;

            foreach (var section in gold.sections) {
                int sectionPoints = section.outputs?.points?.Count ?? 0;
                if (sectionPoints == 0) continue;

                uint nodeId = section.nodeId;
                Assert.IsTrue(track.NodeToSection.TryGetValue(nodeId, out int sectionIndex),
                    $"No section found for nodeId {nodeId} ({section.nodeType})");

                var trackSection = track.Sections[sectionIndex];
                var sectionPointsArray = track.Points.AsArray().GetSubArray(trackSection.StartIndex, trackSection.Length);

                bool isCopyPath = section.nodeType == "CopyPathSection";
                float tolerance = (isCopyPath || afterCopyPath) ? 0.5f : 5e-2f;
                AssertEndpointsMatch(sectionPointsArray, section.outputs.points, nodeId, section.nodeType, tolerance);

                if (isCopyPath) afterCopyPath = true;
            }
        }

        private static void AssertEndpointsMatch(
            NativeArray<KexEdit.Sim.Point> actual,
            System.Collections.Generic.List<GoldPointData> expected,
            uint nodeId,
            string nodeType,
            float tolerance
        ) {
            Assert.Greater(actual.Length, 0, $"{nodeType} {nodeId}: no points generated");
            Assert.Greater(expected.Count, 0, $"{nodeType} {nodeId}: no gold points");

            const float pointCountTolerance = 0.15f;
            float countRatio = (float)actual.Length / expected.Count;
            Assert.IsTrue(
                countRatio >= (1f - pointCountTolerance) && countRatio <= (1f + pointCountTolerance),
                $"{nodeType} {nodeId}: point count diverged too much - actual={actual.Length}, gold={expected.Count}, ratio={countRatio:F3}"
            );

            var actualFirst = actual[0];
            var goldFirst = expected[0];
            AssertPointNear(actualFirst, goldFirst, $"{nodeType} {nodeId} first point", tolerance);

            var actualLast = actual[actual.Length - 1];
            var goldLast = expected[expected.Count - 1];
            AssertPointNear(actualLast, goldLast, $"{nodeType} {nodeId} last point", tolerance);

            Debug.Log($"{nodeType} {nodeId}: endpoints match (actual={actual.Length} pts, gold={expected.Count} pts)");
        }

        private static void AssertPointNear(
            KexEdit.Sim.Point actual,
            GoldPointData expected,
            string context,
            float tolerance
        ) {
            Assert.AreEqual(expected.heartPosition.x, actual.HeartPosition.x, tolerance, $"{context}.HeartPosition.x");
            Assert.AreEqual(expected.heartPosition.y, actual.HeartPosition.y, tolerance, $"{context}.HeartPosition.y");
            Assert.AreEqual(expected.heartPosition.z, actual.HeartPosition.z, tolerance, $"{context}.HeartPosition.z");
            Assert.AreEqual(expected.direction.x, actual.Direction.x, tolerance, $"{context}.Direction.x");
            Assert.AreEqual(expected.direction.y, actual.Direction.y, tolerance, $"{context}.Direction.y");
            Assert.AreEqual(expected.direction.z, actual.Direction.z, tolerance, $"{context}.Direction.z");
        }
    }
}
