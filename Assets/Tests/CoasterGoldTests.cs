using KexEdit.Coaster;
using KexEdit.Legacy.Serialization;
using KexEdit.LegacyImport;
using NUnit.Framework;
using System.IO;
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
        [Ignore("Blocked: cumulative drift investigation needed - first 5 sections pass but ForceSection 2 drifts")]
        public void Veloci_LoadAndEvaluate_MatchesGoldData() {
            RunParityTest("veloci");
        }

        [Test]
        [Ignore("Blocked: CopyPath velocity divergence at low velocity - see PLAN.md Phase 1")]
        public void AllTypes_LoadAndEvaluate_MatchesGoldData() {
            RunParityTest("all_types");
        }

        [Test]
        [Ignore("Blocked: Bridge lateral force calculation bug - see PLAN.md Phase 1")]
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
            for (int i = 0; i < coaster.Graph.NodeCount; i++) {
                uint nodeId = coaster.Graph.NodeIds[i];
                uint nodeType = coaster.Graph.NodeTypes[i];
                string typeName = ((KexEdit.Nodes.NodeType)nodeType).ToString();
                string durInfo = coaster.Durations.TryGetValue(nodeId, out var dur) ? $"dur={dur.Value}" : "no-dur";
                string anchorInfo = coaster.Anchors.TryGetValue(nodeId, out var anc) ? $"vel={anc.Velocity:F2} e={anc.Energy:F2} h={anc.HeartOffset:F2} f={anc.Friction:F4} r={anc.Resistance:F4}" : "no-anchor";
                UnityEngine.Debug.Log($"  [{i}] id={nodeId} type={typeName} {durInfo} {anchorInfo}");
            }

            // Print anchor node 6 data (the source for first section)
            if (coaster.Anchors.TryGetValue(6, out var anchorNodeData)) {
                UnityEngine.Debug.Log($"=== ANCHOR NODE 6 DATA ===");
                UnityEngine.Debug.Log($"  vel={anchorNodeData.Velocity:F4} frictionOrig={anchorNodeData.FrictionOrigin:F4} dir=({anchorNodeData.Direction.x:F3},{anchorNodeData.Direction.y:F3},{anchorNodeData.Direction.z:F3})");
            }

            // Compare with gold first section's input anchor
            var firstGoldAnc = gold.sections[0].inputs?.anchor;
            if (firstGoldAnc != null) {
                UnityEngine.Debug.Log($"=== GOLD FIRST SECTION INPUT ANCHOR ===");
                UnityEngine.Debug.Log($"  vel={firstGoldAnc.velocity:F4} frictionOrig={firstGoldAnc.frictionCompensation:F4} dir=({firstGoldAnc.direction.x:F3},{firstGoldAnc.direction.y:F3},{firstGoldAnc.direction.z:F3})");
            }
        }

        private static void AssertSectionsMatchByType(GoldTrackData gold, EvaluationResult result, string nodeType) {
            int sectionsChecked = 0;
            int cumulativePoints = 0;

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

                SimPointComparer.AssertMatchesGold(path, section.outputs.points, cumulativePoints);
                cumulativePoints += sectionPoints;
                sectionsChecked++;
            }

            Assert.Greater(sectionsChecked, 0, $"No {nodeType} sections with points found in gold data");
            UnityEngine.Debug.Log($"Verified {sectionsChecked} {nodeType} sections point-by-point");
        }

        private static void AssertAllSectionsMatch(GoldTrackData gold, EvaluationResult result) {
            int sectionsChecked = 0;
            int cumulativePoints = 0;

            foreach (var section in gold.sections) {
                int sectionPoints = section.outputs?.points?.Count ?? 0;

                if (sectionPoints == 0) {
                    continue;
                }

                uint nodeId = section.nodeId;
                Assert.IsTrue(result.Paths.TryGetValue(nodeId, out var path),
                    $"No path found for nodeId {nodeId} ({section.nodeType})");

                var goldDur = section.inputs?.duration;
                UnityEngine.Debug.Log($"Checking {section.nodeType} nodeId={nodeId}: {path.Length} points (gold expects {sectionPoints}, gold duration={goldDur?.value} {goldDur?.type}, cumulative offset={cumulativePoints})");

                // Print first few points for debugging
                int debugCount = Unity.Mathematics.math.min(3, path.Length);
                for (int i = 0; i < debugCount; i++) {
                    var p = path[i];
                    var g = section.outputs.points[i];
                    UnityEngine.Debug.Log($"[{i}] actual: pos=({p.HeartPosition.x:F3},{p.HeartPosition.y:F3},{p.HeartPosition.z:F3}) dir=({p.Direction.x:F3},{p.Direction.y:F3},{p.Direction.z:F3}) vel={p.Velocity:F4}");
                    UnityEngine.Debug.Log($"[{i}] gold:   pos=({g.HeartPosition.x:F3},{g.HeartPosition.y:F3},{g.HeartPosition.z:F3}) dir=({g.direction.x:F3},{g.direction.y:F3},{g.direction.z:F3}) vel={g.velocity:F4}");
                }

                SimPointComparer.AssertMatchesGold(path, section.outputs.points, cumulativePoints);
                cumulativePoints += sectionPoints;
                sectionsChecked++;
            }

            Assert.Greater(sectionsChecked, 0, "No sections with points found in gold data");
            UnityEngine.Debug.Log($"Verified {sectionsChecked} sections point-by-point (total {cumulativePoints} points)");
        }
    }
}
