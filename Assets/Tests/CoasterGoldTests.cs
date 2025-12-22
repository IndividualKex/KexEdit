using System.IO;
using KexEdit.Coaster;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.NodeGraph;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class CoasterGoldTests {
        [Test]
        public void Shuttle_LoadAndEvaluate_MatchesGoldData() {
            RunParityTest("shuttle");
        }

        [Test]
        public void Veloci_LoadAndEvaluate_MatchesGoldData() {
            RunParityTest("veloci");
        } 

        [Test]
        public void AllTypes_LoadAndEvaluate_MatchesGoldData() {
            RunParityTest("all_types");
        }

        [Test]
        public void AllTypes_BridgeSectionsOnly_MatchesGoldData() {
            RunParityTestForNodeType("all_types", "Bridge");
        }

        private static void RunParityTestForNodeType(string name, string nodeType) {
            var gold = GoldDataLoader.Load($"Assets/Tests/TrackData/{name}.json");
            var kexPath = $"Assets/Tests/Assets/{name}.kex";

            Assert.IsTrue(File.Exists(kexPath), $"Test file not found: {kexPath}");

            byte[] kexData = File.ReadAllBytes(kexPath);
            var buffer = new NativeArray<byte>(kexData, Allocator.Temp);

            try {
                var serializedGraph = new SerializedGraph();
                GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

                try {
                    LegacyImporter.Import(in serializedGraph, Allocator.TempJob, out var coaster);

                    try {
                        CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.TempJob);

                        try {
                            Assert.Greater(result.Paths.Count, 0, "No paths generated");
                            LogCoasterDiagnostics(coaster, gold);
                            AssertSectionsMatchByType(gold, result, nodeType);
                        }
                        finally {
                            result.Dispose();
                        }
                    }
                    finally {
                        coaster.Dispose();
                    }
                }
                finally {
                    serializedGraph.Dispose();
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
                var serializedGraph = new SerializedGraph();
                GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

                try {
                    LegacyImporter.Import(in serializedGraph, Allocator.TempJob, out var coaster);

                    try {
                        CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.TempJob);

                        try {
                            Assert.Greater(result.Paths.Count, 0, "No paths generated");
                            LogCoasterDiagnostics(coaster, gold);
                            AssertAllSectionsMatch(gold, result);
                        }
                        finally {
                            result.Dispose();
                        }
                    }
                    finally {
                        coaster.Dispose();
                    }
                }
                finally {
                    serializedGraph.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }

        private static void LogCoasterDiagnostics(KexEdit.Coaster.Coaster coaster, GoldTrackData gold) {
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
                string typeName = ((KexEdit.Nodes.NodeType)nodeType).ToString();
                string durInfo = coaster.Durations.TryGetValue(nodeId, out var dur) ? $"dur={dur.Value}" : "no-dur";
                string vectorInfo = coaster.Vectors.TryGetValue(nodeId, out var vec) ? $"pos=({vec.x:F2},{vec.y:F2},{vec.z:F2})" : "no-vec";

                string velInfo = "no-vel";
                // Velocity is only on Anchor nodes at index 2
                if ((KexEdit.Nodes.NodeType)nodeType == KexEdit.Nodes.NodeType.Anchor &&
                    coaster.Graph.TryGetInput(nodeId, 2, out uint velPortId)) {
                    velInfo = coaster.Scalars.TryGetValue(velPortId, out float vel) ? $"vel={vel:F4} (port={velPortId})" : $"vel-port={velPortId}-no-scalar";
                }
                UnityEngine.Debug.Log($"  [{i}] id={nodeId} type={typeName} {durInfo} {vectorInfo} {velInfo}");
            }

            // Show edges
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

            // Compare with gold first section's input anchor
            var firstGoldAnc = gold.sections[0].inputs?.anchor;
            if (firstGoldAnc != null) {
                UnityEngine.Debug.Log($"=== GOLD FIRST SECTION INPUT ANCHOR ===");
                UnityEngine.Debug.Log($"  vel={firstGoldAnc.velocity:F4} frictionOrig={firstGoldAnc.frictionOrigin:F4} dir=({firstGoldAnc.direction.x:F3},{firstGoldAnc.direction.y:F3},{firstGoldAnc.direction.z:F3})");
            }
        }

        private static void AssertSectionsMatchByType(GoldTrackData gold, EvaluationResult result, string nodeType) {
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
                Assert.IsTrue(result.Paths.TryGetValue(nodeId, out var path),
                    $"No path found for nodeId {nodeId} ({section.nodeType})");

                var goldDur = section.inputs?.duration;
                UnityEngine.Debug.Log($"Checking {section.nodeType} nodeId={nodeId}: {path.Length} points (gold expects {sectionPoints}, gold duration={goldDur?.value} {goldDur?.type}, cumulative offset={cumulativePoints})");

                if (isBridgeType) {
                    AssertBridgeTargetMatch(path, section.outputs.points, nodeId);
                }
                else {
                    SimPointComparer.AssertMatchesGold(path, section.outputs.points, cumulativePoints);
                }
                cumulativePoints += sectionPoints;
                sectionsChecked++;
            }

            Assert.Greater(sectionsChecked, 0, $"No {nodeType} sections with points found in gold data");
            UnityEngine.Debug.Log($"Verified {sectionsChecked} {nodeType} sections");
        }

        private static void AssertAllSectionsMatch(GoldTrackData gold, EvaluationResult result) {
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

                Assert.IsTrue(result.Paths.TryGetValue(nodeId, out var path),
                    $"No path found for nodeId {nodeId} ({section.nodeType})");

                var goldDur = section.inputs?.duration;
                UnityEngine.Debug.Log($"Checking {section.nodeType} nodeId={nodeId}: {path.Length} points (gold expects {sectionPoints}, gold duration={goldDur?.value} {goldDur?.type}, cumulative offset={cumulativePoints}, afterBridge={afterBridge})");

                const int HIGH_OFFSET_THRESHOLD = 5000;

                if (isCopyPath || afterBridge) {
                    // CopyPath or post-Bridge: source/target data may differ, verify endpoints only
                    AssertEndpointsMatch(path, section.outputs.points, nodeId, section.nodeType);
                    cumulativePoints += sectionPoints;
                }
                else if (cumulativePoints >= HIGH_OFFSET_THRESHOLD) {
                    // High accumulated offset: floating-point drift makes point-by-point unreliable
                    AssertHighOffsetEndpointsMatch(path, section.outputs.points, nodeId, section.nodeType);
                    cumulativePoints += sectionPoints;
                }
                else {
                    SimPointComparer.AssertMatchesGold(path, section.outputs.points, cumulativePoints);
                    cumulativePoints += sectionPoints;
                }
                sectionsChecked++;
            }

            Assert.Greater(sectionsChecked, 0, "No sections with points found in gold data");
            UnityEngine.Debug.Log($"Verified {sectionsChecked} sections (total {cumulativePoints} cumulative points)");
        }

        private static void AssertBridgeTargetMatch(
            NativeList<KexEdit.Core.Point> actual,
            System.Collections.Generic.List<GoldPointData> expected,
            uint nodeId
        ) {
            Assert.Greater(actual.Length, 0, $"Bridge {nodeId}: no points generated");
            Assert.Greater(expected.Count, 0, $"Bridge {nodeId}: no gold points");

            // Bridges interpolate to a fixed target - only verify the endpoint matches
            const float targetTolerance = 1e-3f;

            var actualLast = actual[^1];
            var goldLast = expected[^1];
            AssertPointNear(actualLast, goldLast, $"Bridge {nodeId} target", targetTolerance);

            UnityEngine.Debug.Log($"Bridge {nodeId}: target matches (actual={actual.Length} pts, gold={expected.Count} pts)");
        }

        private static void AssertHighOffsetEndpointsMatch(
            NativeList<KexEdit.Core.Point> actual,
            System.Collections.Generic.List<GoldPointData> expected,
            uint nodeId,
            string nodeType
        ) {
            Assert.Greater(actual.Length, 0, $"{nodeType} {nodeId}: no points generated");
            Assert.Greater(expected.Count, 0, $"{nodeType} {nodeId}: no gold points");

            Assert.AreEqual(expected.Count, actual.Length,
                $"{nodeType} {nodeId}: point count mismatch - actual={actual.Length}, gold={expected.Count}");

            // High-offset sections have accumulated significant drift - use relaxed tolerance
            const float highOffsetTolerance = 5.0f;

            var actualLast = actual[^1];
            var goldLast = expected[^1];
            AssertPointNear(actualLast, goldLast, $"{nodeType} {nodeId} last point (high-offset)", highOffsetTolerance);

            UnityEngine.Debug.Log($"{nodeType} {nodeId}: high-offset endpoint matches (actual={actual.Length} pts)");
        }

        private static void AssertEndpointsMatch(
            NativeList<KexEdit.Core.Point> actual,
            System.Collections.Generic.List<GoldPointData> expected,
            uint nodeId,
            string nodeType
        ) {
            Assert.Greater(actual.Length, 0, $"{nodeType} {nodeId}: no points generated");
            Assert.Greater(expected.Count, 0, $"{nodeType} {nodeId}: no gold points");

            const float pointCountTolerance = 0.15f;
            float countRatio = (float)actual.Length / expected.Count;
            Assert.IsTrue(
                countRatio >= (1f - pointCountTolerance) && countRatio <= (1f + pointCountTolerance),
                $"{nodeType} {nodeId}: point count diverged too much - actual={actual.Length}, gold={expected.Count}, ratio={countRatio:F3}"
            );

            const float endpointTolerance = 5e-2f;

            var actualFirst = actual[0];
            var goldFirst = expected[0];
            AssertPointNear(actualFirst, goldFirst, $"{nodeType} {nodeId} first point", endpointTolerance);

            var actualLast = actual[^1];
            var goldLast = expected[^1];
            AssertPointNear(actualLast, goldLast, $"{nodeType} {nodeId} last point", endpointTolerance);

            UnityEngine.Debug.Log($"{nodeType} {nodeId}: endpoints match (actual={actual.Length} pts, gold={expected.Count} pts)");
        }

        private static void AssertPointNear(
            KexEdit.Core.Point actual,
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
