using System.IO;
using System.Text;
using KexEdit.Document;
using KexEdit.Legacy;
using KexEdit.Graph.Typed;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Nodes.Bridge;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using CoreNodeType = KexEdit.Sim.Schema.NodeType;
using TrackData = KexEdit.Track.Track;

namespace Tests {
    [TestFixture]
    [Category("Investigation")]
    public class BridgeDeserializationTests {
        [Test]
        [TestCase("Assets/Tests/Assets/all_types.kex")]
        // [TestCase("Assets/Tests/Assets/veloci.kex")] // Disabled - veloci test
        [TestCase("Assets/Tests/Assets/shuttle.kex")]
        public void DumpCoasterAfterImport(string path) {
            var bytes = File.ReadAllBytes(path);
            var buffer = new NativeArray<byte>(bytes, Allocator.Temp);

            LegacyImporter.Import(ref buffer, Allocator.Temp, out var coaster, out _);

            var sb = new StringBuilder();
            sb.AppendLine("=== COASTER AFTER IMPORT ===");
            sb.AppendLine($"Nodes: {coaster.Graph.NodeIds.Length}");
            sb.AppendLine($"Ports: {coaster.Graph.PortIds.Length}");
            sb.AppendLine($"Edges: {coaster.Graph.EdgeIds.Length}");

            sb.AppendLine("\n=== BRIDGE NODES IN COASTER ===");
            for (int i = 0; i < coaster.Graph.NodeIds.Length; i++) {
                uint nodeId = coaster.Graph.NodeIds[i];
                var nodeType = (CoreNodeType)coaster.Graph.NodeTypes[i];
                if (nodeType != CoreNodeType.Bridge) continue;

                sb.AppendLine($"\nBridge Node {nodeId}:");
                sb.AppendLine($"  Position: {coaster.Graph.NodePositions[i]}");
                sb.AppendLine($"  Input count: {coaster.Graph.NodeInputCount[i]}");
                sb.AppendLine($"  Output count: {coaster.Graph.NodeOutputCount[i]}");

                sb.AppendLine($"  Schema expects:");
                sb.AppendLine($"    Input[0] = Anchor (PortDataType.Anchor, 0)");
                sb.AppendLine($"    Input[1] = Target (PortDataType.Anchor, 1)");
                sb.AppendLine($"    Input[2] = OutWeight (PortDataType.Scalar, 1)");
                sb.AppendLine($"    Input[3] = InWeight (PortDataType.Scalar, 0)");

                sb.AppendLine($"  Actual input ports:");
                for (int inputIdx = 0; inputIdx < 4; inputIdx++) {
                    if (coaster.Graph.TryGetInput(nodeId, inputIdx, out uint portId)) {
                        int portIndex = coaster.Graph.PortIndexMap[portId];
                        PortSpec.FromEncoded(coaster.Graph.PortTypes[portIndex], out var spec);
                        sb.AppendLine($"    Input[{inputIdx}]: Port {portId} = {spec.DataType}, LocalIndex={spec.LocalIndex}");

                        bool connected = false;
                        for (int e = 0; e < coaster.Graph.EdgeIds.Length; e++) {
                            if (coaster.Graph.EdgeTargets[e] == portId) {
                                uint sourcePortId = coaster.Graph.EdgeSources[e];
                                int srcPortIdx = coaster.Graph.PortIndexMap[sourcePortId];
                                uint srcNodeId = coaster.Graph.PortOwners[srcPortIdx];
                                int srcNodeIdx = coaster.Graph.NodeIndexMap[srcNodeId];
                                var srcNodeType = (CoreNodeType)coaster.Graph.NodeTypes[srcNodeIdx];
                                sb.AppendLine($"      Connected from: Node {srcNodeId} ({srcNodeType})");
                                connected = true;
                            }
                        }
                        if (!connected) {
                            sb.AppendLine($"      NOT CONNECTED");
                        }
                    }
                    else {
                        sb.AppendLine($"    Input[{inputIdx}]: MISSING");
                    }
                }

                if (coaster.Graph.TryGetInput(nodeId, BridgePorts.Target, out uint targetPortId)) {
                    bool hasTargetConnection = false;
                    for (int e = 0; e < coaster.Graph.EdgeIds.Length; e++) {
                        if (coaster.Graph.EdgeTargets[e] == targetPortId) {
                            hasTargetConnection = true;
                            break;
                        }
                    }
                    sb.AppendLine($"  Target port (input[1]) connected: {hasTargetConnection}");
                }
            }

            UnityEngine.Debug.Log(sb.ToString());

            coaster.Dispose();
            buffer.Dispose();

            Assert.Pass("Coaster structure dumped to console");
        }

        [Test]
        public void VerifyBridgePortSpecSchema() {
            var sb = new StringBuilder();
            sb.AppendLine("=== BRIDGE PORT SCHEMA ===");

            for (int i = 0; i < NodeSchema.InputCount(CoreNodeType.Bridge); i++) {
                NodeSchema.InputSpec(CoreNodeType.Bridge, i, out var spec);
                NodeSchema.InputName(CoreNodeType.Bridge, i, out var name);
                sb.AppendLine($"Input[{i}] = {name} ({spec.DataType}, LocalIndex={spec.LocalIndex})");
            }

            for (int i = 0; i < NodeSchema.OutputCount(CoreNodeType.Bridge); i++) {
                NodeSchema.OutputSpec(CoreNodeType.Bridge, i, out var spec);
                NodeSchema.OutputName(CoreNodeType.Bridge, i, out var name);
                sb.AppendLine($"Output[{i}] = {name} ({spec.DataType}, LocalIndex={spec.LocalIndex})");
            }

            sb.AppendLine();
            sb.AppendLine("=== BRIDGE PORTS CONSTANTS ===");
            sb.AppendLine($"BridgePorts.Anchor = {BridgePorts.Anchor}");
            sb.AppendLine($"BridgePorts.Target = {BridgePorts.Target}");
            sb.AppendLine($"BridgePorts.OutWeight = {BridgePorts.OutWeight}");
            sb.AppendLine($"BridgePorts.InWeight = {BridgePorts.InWeight}");

            UnityEngine.Debug.Log(sb.ToString());
            Assert.Pass("Schema dumped to console");
        }

        [Test]
        public void VerifyLegacyPortTypeToSchemaMapping() {
            var sb = new StringBuilder();
            sb.AppendLine("=== LEGACY PORT TYPE VALUES ===");
            sb.AppendLine($"PortType.Anchor = {(int)PortType.Anchor}");
            sb.AppendLine($"PortType.InWeight = {(int)PortType.InWeight}");
            sb.AppendLine($"PortType.OutWeight = {(int)PortType.OutWeight}");

            sb.AppendLine();
            sb.AppendLine("Bridge node in legacy format typically has:");
            sb.AppendLine("  Input[0]: Anchor");
            sb.AppendLine("  Input[1]: Anchor (Target - if present)");
            sb.AppendLine("  Input[2]: OutWeight");
            sb.AppendLine("  Input[3]: InWeight");

            UnityEngine.Debug.Log(sb.ToString());
            Assert.Pass();
        }

        [Test]
        [TestCase("Assets/Tests/Assets/all_types.kex")]
        // [TestCase("Assets/Tests/Assets/veloci.kex")] // Disabled - veloci test
        [TestCase("Assets/Tests/Assets/shuttle.kex")]
        public void EvaluateBridgeNodesSuccessfully(string path) {
            var bytes = File.ReadAllBytes(path);
            var buffer = new NativeArray<byte>(bytes, Allocator.Temp);

            LegacyImporter.Import(ref buffer, Allocator.TempJob, out var coaster, out _);

            TrackData.Build(in coaster, Allocator.TempJob, 0.1f, 0, out var track);

            var sb = new StringBuilder();
            sb.AppendLine($"=== EVALUATION RESULT FOR {path} ===");
            sb.AppendLine($"Sections: {track.SectionCount}");
            sb.AppendLine($"NodeToSection mappings: {track.NodeToSection.Count}");

            int bridgePathCount = 0;
            for (int i = 0; i < coaster.Graph.NodeIds.Length; i++) {
                uint nodeId = coaster.Graph.NodeIds[i];
                var nodeType = (CoreNodeType)coaster.Graph.NodeTypes[i];
                if (nodeType != CoreNodeType.Bridge) continue;

                sb.AppendLine($"\nBridge Node {nodeId}:");
                bool hasSection = track.NodeToSection.TryGetValue(nodeId, out int sectionIdx);
                int pathLen = 0;
                if (hasSection && sectionIdx >= 0 && sectionIdx < track.Sections.Length) {
                    var section = track.Sections[sectionIdx];
                    pathLen = section.IsValid ? section.Length : 0;
                }

                sb.AppendLine($"  Has section: {hasSection} (index: {(hasSection ? sectionIdx : -1)})");
                sb.AppendLine($"  Section length: {pathLen}");

                if (hasSection && pathLen > 0) {
                    bridgePathCount++;
                }
            }

            UnityEngine.Debug.Log(sb.ToString());

            // Verify that Bridge nodes got evaluated
            int totalBridgeNodes = 0;
            for (int i = 0; i < coaster.Graph.NodeIds.Length; i++) {
                if ((CoreNodeType)coaster.Graph.NodeTypes[i] == CoreNodeType.Bridge) {
                    totalBridgeNodes++;
                }
            }

            if (totalBridgeNodes > 0) {
                Assert.AreEqual(totalBridgeNodes, bridgePathCount,
                    $"Expected all {totalBridgeNodes} Bridge nodes to have paths, but only {bridgePathCount} have paths");
            }

            track.Dispose();
            coaster.Dispose();
            buffer.Dispose();
        }
    }
}
