using KexEdit.Nodes;
using KexGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.NodeGraph {
    [BurstCompile]
    public static class TypedGraphExtensions {
        [BurstCompile]
        public static uint CreateNode(
            ref this Graph graph,
            NodeType nodeType,
            in float2 position,
            out NativeArray<uint> inputPortIds,
            out NativeArray<uint> outputPortIds,
            Allocator allocator
        ) {
            uint nodeId = graph.AddNode((uint)nodeType, position);

            int inputCount = NodeSchema.InputCount(nodeType);
            int outputCount = NodeSchema.OutputCount(nodeType);

            inputPortIds = new NativeArray<uint>(inputCount, allocator);
            outputPortIds = new NativeArray<uint>(outputCount, allocator);

            for (int i = 0; i < inputCount; i++) {
                PortId portType = NodeSchema.Input(nodeType, i);
                inputPortIds[i] = graph.AddInputPort(nodeId, (uint)portType);
            }

            for (int i = 0; i < outputCount; i++) {
                PortId portType = NodeSchema.Output(nodeType, i);
                outputPortIds[i] = graph.AddOutputPort(nodeId, (uint)portType);
            }

            return nodeId;
        }

        [BurstCompile]
        public static bool TryGetNodeType(in this Graph graph, uint nodeId, out NodeType nodeType) {
            if (!graph.TryGetNodeIndex(nodeId, out int index)) {
                nodeType = default;
                return false;
            }
            nodeType = (NodeType)graph.NodeTypes[index];
            return true;
        }

        [BurstCompile]
        public static bool TryGetPortType(in this Graph graph, uint portId, out PortId portType) {
            if (!graph.TryGetPortIndex(portId, out int index)) {
                portType = default;
                return false;
            }
            portType = (PortId)graph.PortTypes[index];
            return true;
        }

        [BurstCompile]
        public static bool TryGetInputPort(
            in this Graph graph, uint nodeId, PortId targetPortType, out uint portId
        ) {
            portId = 0;
            if (!graph.TryGetNodeType(nodeId, out NodeType nodeType)) return false;

            int inputCount = NodeSchema.InputCount(nodeType);
            graph.GetInputPorts(nodeId, out var inputPorts, Allocator.Temp);

            for (int i = 0; i < inputCount; i++) {
                if (NodeSchema.Input(nodeType, i) == targetPortType) {
                    portId = inputPorts[i];
                    inputPorts.Dispose();
                    return true;
                }
            }

            inputPorts.Dispose();
            return false;
        }

        [BurstCompile]
        public static bool TryGetOutputPort(
            in this Graph graph, uint nodeId, PortId targetPortType, out uint portId
        ) {
            portId = 0;
            if (!graph.TryGetNodeType(nodeId, out NodeType nodeType)) return false;

            int outputCount = NodeSchema.OutputCount(nodeType);
            graph.GetOutputPorts(nodeId, out var outputPorts, Allocator.Temp);

            for (int i = 0; i < outputCount; i++) {
                if (NodeSchema.Output(nodeType, i) == targetPortType) {
                    portId = outputPorts[i];
                    outputPorts.Dispose();
                    return true;
                }
            }

            outputPorts.Dispose();
            return false;
        }

        [BurstCompile]
        public static void RemoveNodeCascade(ref this Graph graph, uint nodeId) {
            graph.GetInputPorts(nodeId, out var inputs, Allocator.Temp);
            graph.GetOutputPorts(nodeId, out var outputs, Allocator.Temp);

            for (int i = 0; i < inputs.Length; i++) {
                RemoveEdgesForPort(ref graph, inputs[i]);
            }
            for (int i = 0; i < outputs.Length; i++) {
                RemoveEdgesForPort(ref graph, outputs[i]);
            }

            for (int i = 0; i < inputs.Length; i++) graph.RemovePort(inputs[i]);
            for (int i = 0; i < outputs.Length; i++) graph.RemovePort(outputs[i]);

            inputs.Dispose();
            outputs.Dispose();

            graph.RemoveNode(nodeId);
        }

        [BurstCompile]
        static void RemoveEdgesForPort(ref Graph graph, uint portId) {
            var toRemove = new NativeList<uint>(Allocator.Temp);
            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                if (graph.EdgeSources[i] == portId || graph.EdgeTargets[i] == portId) {
                    toRemove.Add(graph.EdgeIds[i]);
                }
            }
            for (int i = 0; i < toRemove.Length; i++) {
                graph.RemoveEdge(toRemove[i]);
            }
            toRemove.Dispose();
        }
    }
}
