using System.IO;
using KexEdit.Legacy;
using KexEdit.Graph.Typed;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using NodeMeta = KexEdit.Document.NodeMeta;
using Coaster = KexEdit.Document.Document;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class CoasterGoldTests {
        [Test]
        public void Shuttle_LoadAndEvaluate_MatchesGoldData() {
            RunParityTest("shuttle");
        }

        [Test]
        public void Circuit_LoadAndEvaluate_MatchesGoldData() {
            RunParityTest("circuit");
        } 

        [Test]
        public void AllTypes_LoadAndEvaluate_MatchesGoldData() {
            RunParityTest("all_types");
        }

        [Test]
        public void AllTypes_BridgeSectionsOnly_MatchesGoldData() {
            RunParityTestForNodeType("all_types", "Bridge");
        }


        [Test]
        public void Switch_LoadAndEvaluate_EndpointsMatch() {
            RunSwitchParityTest("switch");
        }

        private static void RunSwitchParityTest(string name) {
            var gold = GoldDataLoader.Load($"Assets/Tests/TrackData/{name}.json");
            var kexPath = $"Assets/Tests/Assets/{name}.kex";

            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);

                try {
                    KexEdit.Track.Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

                    try {
                        Assert.Greater(track.SectionCount, 0, "No sections generated");

                        bool afterCopyPath = false;
                        foreach (var section in gold.sections) {
                            int sectionPoints = section.outputs?.points?.Count ?? 0;
                            if (sectionPoints == 0) continue;

                            uint nodeId = section.nodeId;
                            Assert.IsTrue(track.NodeToSection.TryGetValue(nodeId, out int sectionIndex),
                                $"No section found for nodeId {nodeId} ({section.nodeType})");

                            var trackSection = track.Sections[sectionIndex];
                            var sectionPoints_array = track.Points.AsArray().GetSubArray(trackSection.StartIndex, trackSection.Length);

                            bool isCopyPath = section.nodeType == "CopyPathSection";
                            float tolerance = (isCopyPath || afterCopyPath) ? 0.5f : 5e-2f;
                            AssertEndpointsMatchWithTolerance(sectionPoints_array, section.outputs.points, nodeId, section.nodeType, tolerance);

                            if (isCopyPath) afterCopyPath = true;
                        }
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

        private static void RunParityTestForNodeType(string name, string nodeType) {
            var gold = GoldDataLoader.Load($"Assets/Tests/TrackData/{name}.json");
            var kexPath = $"Assets/Tests/Assets/{name}.kex";

            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);

                try {
                    KexEdit.Track.Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

                    try {
                        Assert.Greater(track.SectionCount, 0, "No sections generated");
                        LogCoasterDiagnostics(coaster, gold);
                        AssertSectionsMatchByType(gold, track, nodeType);
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

        private static void RunParityTest(string name) {
            var gold = GoldDataLoader.Load($"Assets/Tests/TrackData/{name}.json");
            var kexPath = $"Assets/Tests/Assets/{name}.kex";

            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);

                try {
                    KexEdit.Track.Track.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

                    try {
                        Assert.Greater(track.SectionCount, 0, "No sections generated");
                        LogCoasterDiagnostics(coaster, gold);
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

        private static void LogCoasterDiagnostics(Coaster coaster, GoldTrackData gold) {
            UnityEngine.Debug.Log($"=== COASTER DIAGNOSTICS ===");
            UnityEngine.Debug.Log($"Graph: {coaster.Graph.NodeCount} nodes, {coaster.Graph.EdgeCount} edges");

            UnityEngine.Debug.Log($"=== ALL NODES IN GRAPH ===");
            UnityEngine.Debug.Log($"=== SCALARS DUMP ({coaster.Scalars.Count} entries) ===");
            var scalarKeys = coaster.Scalars.GetKeyArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < scalarKeys.Length; i++) {
                UnityEngine.Debug.Log($"  Scalar[{scalarKeys[i]}] = {coaster.Scalars[scalarKeys[i]]:F4}");
            }
            scalarKeys.Dispose();

            for (int i = 0; i < coaster.Graph.NodeCount; i++) {
                uint nodeId = coaster.Graph.NodeIds[i];
                uint nodeType = coaster.Graph.NodeTypes[i];
                string typeName = ((KexEdit.Sim.Schema.NodeType)nodeType).ToString();
                ulong durKey = Coaster.InputKey(nodeId, NodeMeta.Duration);
                string durInfo = coaster.Scalars.TryGetValue(durKey, out var dur) ? $"dur={dur}" : "no-dur";
                string vectorInfo = coaster.Vectors.TryGetValue(nodeId, out var vec) ? $"pos=({vec.x:F2},{vec.y:F2},{vec.z:F2})" : "no-vec";

                string velInfo = "no-vel";
                if ((KexEdit.Sim.Schema.NodeType)nodeType == KexEdit.Sim.Schema.NodeType.Anchor &&
                    coaster.Graph.TryGetInput(nodeId, 2, out uint velPortId)) {
                    velInfo = coaster.Scalars.TryGetValue(velPortId, out float vel) ? $"vel={vel:F4} (port={velPortId})" : $"vel-port={velPortId}-no-scalar";
                }
                UnityEngine.Debug.Log($"  [{i}] id={nodeId} type={typeName} {durInfo} {vectorInfo} {velInfo}");
            }

            UnityEngine.Debug.Log($"=== EDGES ({coaster.Graph.EdgeCount}) ===");
            for (int i = 0; i < coaster.Graph.EdgeCount; i++) {
                uint sourcePortId = coaster.Graph.EdgeSources[i];
                uint targetPortId = coaster.Graph.EdgeTargets[i];
                if (!coaster.Graph.TryGetPortIndex(sourcePortId, out int sourceIdx)) continue;
                if (!coaster.Graph.TryGetPortIndex(targetPortId, out int targetIdx)) continue;
                uint sourceNodeId = coaster.Graph.PortOwners[sourceIdx];
                uint targetNodeId = coaster.Graph.PortOwners[targetIdx];
                UnityEngine.Debug.Log($"  Edge: node {sourceNodeId} (port={sourcePortId}) -> node {targetNodeId} (port={targetPortId})");
            }

            var firstGoldAnc = gold.sections[0].inputs?.anchor;
            if (firstGoldAnc != null) {
                UnityEngine.Debug.Log($"=== GOLD FIRST SECTION INPUT ANCHOR ===");
                UnityEngine.Debug.Log($"  vel={firstGoldAnc.velocity:F4} frictionOrig={firstGoldAnc.frictionOrigin:F4} dir=({firstGoldAnc.direction.x:F3},{firstGoldAnc.direction.y:F3},{firstGoldAnc.direction.z:F3})");
            }
        }

        private static void AssertSectionsMatchByType(GoldTrackData gold, KexEdit.Track.Track track, string nodeType) {
            int sectionsChecked = 0;
            int cumulativePoints = 0;
            bool isBridgeType = nodeType == "Bridge";

            foreach (var section in gold.sections) {
                int sectionPoints = section.outputs?.points?.Count ?? 0;

                if (section.nodeType != nodeType) {
                    cumulativePoints += sectionPoints;
                    continue;
                }

                if (sectionPoints == 0) {
                    continue;
                }

                uint nodeId = section.nodeId;
                Assert.IsTrue(track.NodeToSection.TryGetValue(nodeId, out int sectionIndex),
                    $"No section found for nodeId {nodeId} ({section.nodeType})");

                var trackSection = track.Sections[sectionIndex];
                var sectionPoints_array = track.Points.AsArray().GetSubArray(trackSection.StartIndex, trackSection.Length);

                var goldDur = section.inputs?.duration;
                UnityEngine.Debug.Log($"Checking {section.nodeType} nodeId={nodeId}: {sectionPoints_array.Length} points (gold expects {sectionPoints}, gold duration={goldDur?.value} {goldDur?.type}, cumulative offset={cumulativePoints})");

                if (isBridgeType) {
                    AssertBridgeTargetMatch(sectionPoints_array, section.outputs.points, nodeId);
                }
                else {
                    SimPointComparer.AssertMatchesGold(sectionPoints_array, section.outputs.points, cumulativePoints);
                }
                cumulativePoints += sectionPoints;
                sectionsChecked++;
            }

            Assert.Greater(sectionsChecked, 0, $"No {nodeType} sections with points found in gold data");
            UnityEngine.Debug.Log($"Verified {sectionsChecked} {nodeType} sections");
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
                    UnityEngine.Debug.Log($"Skipping Bridge nodeId={nodeId}: legacy .kex format doesn't store target anchor data");
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
                var sectionPoints_array = track.Points.AsArray().GetSubArray(trackSection.StartIndex, trackSection.Length);

                var goldDur = section.inputs?.duration;
                UnityEngine.Debug.Log($"Checking {section.nodeType} nodeId={nodeId}: {sectionPoints_array.Length} points (gold expects {sectionPoints}, gold duration={goldDur?.value} {goldDur?.type}, cumulative offset={cumulativePoints}, afterBridge={afterBridge})");

                const int HIGH_OFFSET_THRESHOLD = 5000;

                if (isCopyPath || afterBridge) {
                    AssertEndpointsMatch(sectionPoints_array, section.outputs.points, nodeId, section.nodeType);
                    cumulativePoints += sectionPoints;
                }
                else if (cumulativePoints >= HIGH_OFFSET_THRESHOLD) {
                    AssertHighOffsetEndpointsMatch(sectionPoints_array, section.outputs.points, nodeId, section.nodeType);
                    cumulativePoints += sectionPoints;
                }
                else {
                    SimPointComparer.AssertMatchesGold(sectionPoints_array, section.outputs.points, cumulativePoints);
                    cumulativePoints += sectionPoints;
                }
                sectionsChecked++;
            }

            Assert.Greater(sectionsChecked, 0, "No sections with points found in gold data");
            UnityEngine.Debug.Log($"Verified {sectionsChecked} sections (total {cumulativePoints} cumulative points)");
        }

        private static void AssertBridgeTargetMatch(
            NativeArray<KexEdit.Sim.Point> actual,
            System.Collections.Generic.List<GoldPointData> expected,
            uint nodeId
        ) {
            Assert.Greater(actual.Length, 0, $"Bridge {nodeId}: no points generated");
            Assert.Greater(expected.Count, 0, $"Bridge {nodeId}: no gold points");

            const float targetTolerance = 1e-3f;

            var actualLast = actual[actual.Length - 1];
            var goldLast = expected[expected.Count - 1];
            AssertPointNear(actualLast, goldLast, $"Bridge {nodeId} target", targetTolerance);

            UnityEngine.Debug.Log($"Bridge {nodeId}: target matches (actual={actual.Length} pts, gold={expected.Count} pts)");
        }

        private static void AssertHighOffsetEndpointsMatch(
            NativeArray<KexEdit.Sim.Point> actual,
            System.Collections.Generic.List<GoldPointData> expected,
            uint nodeId,
            string nodeType
        ) {
            Assert.Greater(actual.Length, 0, $"{nodeType} {nodeId}: no points generated");
            Assert.Greater(expected.Count, 0, $"{nodeType} {nodeId}: no gold points");

            Assert.AreEqual(expected.Count, actual.Length,
                $"{nodeType} {nodeId}: point count mismatch - actual={actual.Length}, gold={expected.Count}");

            const float highOffsetTolerance = 5.0f;

            var actualLast = actual[actual.Length - 1];
            var goldLast = expected[expected.Count - 1];
            AssertPointNear(actualLast, goldLast, $"{nodeType} {nodeId} last point (high-offset)", highOffsetTolerance);

            UnityEngine.Debug.Log($"{nodeType} {nodeId}: high-offset endpoint matches (actual={actual.Length} pts)");
        }

        private static void AssertEndpointsMatch(
            NativeArray<KexEdit.Sim.Point> actual,
            System.Collections.Generic.List<GoldPointData> expected,
            uint nodeId,
            string nodeType
        ) {
            AssertEndpointsMatchWithTolerance(actual, expected, nodeId, nodeType, 5e-2f);
        }

        private static void AssertEndpointsMatchWithTolerance(
            NativeArray<KexEdit.Sim.Point> actual,
            System.Collections.Generic.List<GoldPointData> expected,
            uint nodeId,
            string nodeType,
            float endpointTolerance
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
            AssertPointNear(actualFirst, goldFirst, $"{nodeType} {nodeId} first point", endpointTolerance);

            var actualLast = actual[actual.Length - 1];
            var goldLast = expected[expected.Count - 1];
            AssertPointNear(actualLast, goldLast, $"{nodeType} {nodeId} last point", endpointTolerance);

            UnityEngine.Debug.Log($"{nodeType} {nodeId}: endpoints match (actual={actual.Length} pts, gold={expected.Count} pts)");
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
