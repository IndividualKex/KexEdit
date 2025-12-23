using System.IO;
using System.Text;
using KexEdit.Coaster;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.NodeGraph;
using KexEdit.Nodes;
using KexEdit.Nodes.Bridge;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using LegacyNodeType = KexEdit.Legacy.NodeType;
using CoreNodeType = KexEdit.Nodes.NodeType;

namespace Tests {
    [TestFixture]
    [Category("Investigation")]
    public class BridgeDeserializationTests {
        [Test]
        [TestCase("Assets/Tests/Assets/all_types.kex")]
        [TestCase("Assets/Tests/Assets/veloci.kex")]
        [TestCase("Assets/Tests/Assets/shuttle.kex")]
        public void DumpGraphStructure(string path) {
            var bytes = File.ReadAllBytes(path);
            var buffer = new NativeArray<byte>(bytes, Allocator.Temp);

            var serializedGraph = new SerializedGraph();
            GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

            var sb = new StringBuilder();
            sb.AppendLine($"File version: {serializedGraph.Version}");
            sb.AppendLine($"Node count: {serializedGraph.Nodes.Length}");
            sb.AppendLine($"Edge count: {serializedGraph.Edges.Length}");
            sb.AppendLine();

            sb.AppendLine("=== NODES ===");
            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                sb.AppendLine($"\nNode {node.Node.Id}: {node.Node.Type}");
                sb.AppendLine($"  Position: ({node.Node.Position.x:F1}, {node.Node.Position.y:F1})");

                sb.AppendLine($"  Input ports ({node.InputPorts.Length}):");
                for (int j = 0; j < node.InputPorts.Length; j++) {
                    var port = node.InputPorts[j];
                    string value = port.Port.Type == PortType.Anchor || port.Port.Type == PortType.Path
                        ? ""
                        : $" = {port.Value.Roll:F3}";
                    sb.AppendLine($"    [{j}] Port {port.Port.Id}: {port.Port.Type}{value}");
                }

                sb.AppendLine($"  Output ports ({node.OutputPorts.Length}):");
                for (int j = 0; j < node.OutputPorts.Length; j++) {
                    var port = node.OutputPorts[j];
                    sb.AppendLine($"    [{j}] Port {port.Port.Id}: {port.Port.Type}");
                }
            }

            sb.AppendLine("\n=== EDGES ===");
            for (int i = 0; i < serializedGraph.Edges.Length; i++) {
                var edge = serializedGraph.Edges[i];
                sb.AppendLine($"Edge {edge.Id}: Port {edge.SourceId} -> Port {edge.TargetId}");
            }

            sb.AppendLine("\n=== BRIDGE NODE ANALYSIS ===");
            for (int i = 0; i < serializedGraph.Nodes.Length; i++) {
                var node = serializedGraph.Nodes[i];
                if (node.Node.Type != LegacyNodeType.Bridge) continue;

                sb.AppendLine($"\nBridge Node {node.Node.Id}:");
                sb.AppendLine($"  Expected schema: [Anchor, Target, OutWeight, InWeight]");
                sb.AppendLine($"  Actual ports:");
                for (int j = 0; j < node.InputPorts.Length; j++) {
                    var port = node.InputPorts[j];
                    sb.AppendLine($"    [{j}] {port.Port.Type} (id={port.Port.Id})");
                }

                sb.AppendLine($"  Connections:");
                for (int j = 0; j < node.InputPorts.Length; j++) {
                    var port = node.InputPorts[j];
                    bool found = false;
                    for (int k = 0; k < serializedGraph.Edges.Length; k++) {
                        if (serializedGraph.Edges[k].TargetId == port.Port.Id) {
                            var edge = serializedGraph.Edges[k];
                            string sourceInfo = $"Port {edge.SourceId}";
                            for (int m = 0; m < serializedGraph.Nodes.Length; m++) {
                                foreach (var outPort in serializedGraph.Nodes[m].OutputPorts) {
                                    if (outPort.Port.Id == edge.SourceId) {
                                        sourceInfo = $"Node {serializedGraph.Nodes[m].Node.Id} ({serializedGraph.Nodes[m].Node.Type})";
                                        break;
                                    }
                                }
                            }
                            sb.AppendLine($"    Input[{j}] ({port.Port.Type}) <- {sourceInfo}");
                            found = true;
                        }
                    }
                    if (!found) {
                        sb.AppendLine($"    Input[{j}] ({port.Port.Type}): NOT CONNECTED");
                    }
                }

                sb.AppendLine($"  Anchor data in node:");
                sb.AppendLine($"    HeartPosition: {node.Anchor.HeartPosition}");
                sb.AppendLine($"    Direction: {node.Anchor.Direction}");
                sb.AppendLine($"    Velocity: {node.Anchor.Velocity}");
            }

            UnityEngine.Debug.Log(sb.ToString());

            serializedGraph.Dispose();
            buffer.Dispose();

            Assert.Pass("Graph structure dumped to console");
        }

        [Test]
        [TestCase("Assets/Tests/Assets/all_types.kex")]
        [TestCase("Assets/Tests/Assets/veloci.kex")]
        [TestCase("Assets/Tests/Assets/shuttle.kex")]
        public void DumpCoasterAfterImport(string path) {
            var bytes = File.ReadAllBytes(path);
            var buffer = new NativeArray<byte>(bytes, Allocator.Temp);

            var serializedGraph = new SerializedGraph();
            GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

            LegacyImporter.Import(in serializedGraph, Allocator.Temp, out var coaster);

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
            serializedGraph.Dispose();
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
        [TestCase("Assets/Tests/Assets/veloci.kex")]
        [TestCase("Assets/Tests/Assets/shuttle.kex")]
        public void EvaluateBridgeNodesSuccessfully(string path) {
            var bytes = File.ReadAllBytes(path);
            var buffer = new NativeArray<byte>(bytes, Allocator.Temp);

            var serializedGraph = new SerializedGraph();
            GraphSerializer.Deserialize(ref serializedGraph, ref buffer);

            LegacyImporter.Import(in serializedGraph, Allocator.TempJob, out var coaster);

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.TempJob);

            var sb = new StringBuilder();
            sb.AppendLine($"=== EVALUATION RESULT FOR {path} ===");
            sb.AppendLine($"Output anchors: {result.OutputAnchors.Count}");
            sb.AppendLine($"Paths: {result.Paths.Count}");

            int bridgePathCount = 0;
            for (int i = 0; i < coaster.Graph.NodeIds.Length; i++) {
                uint nodeId = coaster.Graph.NodeIds[i];
                var nodeType = (CoreNodeType)coaster.Graph.NodeTypes[i];
                if (nodeType != CoreNodeType.Bridge) continue;

                sb.AppendLine($"\nBridge Node {nodeId}:");
                bool hasOutput = result.OutputAnchors.ContainsKey(nodeId);
                bool hasPath = result.Paths.TryGetValue(nodeId, out var bridgePath);
                int pathLen = hasPath && bridgePath.IsCreated ? bridgePath.Length : 0;

                sb.AppendLine($"  Has output anchor: {hasOutput}");
                sb.AppendLine($"  Has path: {hasPath} (length: {pathLen})");

                if (hasPath && pathLen > 0) {
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

            result.Dispose();
            coaster.Dispose();
            serializedGraph.Dispose();
            buffer.Dispose();
        }
    }
}
