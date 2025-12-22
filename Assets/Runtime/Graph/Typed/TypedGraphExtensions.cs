using KexEdit.Sim.Schema;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Graph.Typed {
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
                NodeSchema.InputSpec(nodeType, i, out PortSpec portSpec);
                inputPortIds[i] = graph.AddInputPort(nodeId, portSpec.ToEncoded());
            }

            for (int i = 0; i < outputCount; i++) {
                NodeSchema.OutputSpec(nodeType, i, out PortSpec portSpec);
                outputPortIds[i] = graph.AddOutputPort(nodeId, portSpec.ToEncoded());
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
        public static bool TryGetPortSpec(in this Graph graph, uint portId, out PortSpec portSpec) {
            if (!graph.TryGetPortIndex(portId, out int index)) {
                portSpec = default;
                return false;
            }
            PortSpec.FromEncoded(graph.PortTypes[index], out portSpec);
            return true;
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

        [BurstCompile]
        public static bool TryGetInputBySpec(
            in this Graph graph, uint nodeId, PortDataType dataType, int localIndex, out uint portId
        ) {
            portId = 0;
            graph.GetInputPorts(nodeId, out var inputPorts, Allocator.Temp);

            int matchCount = 0;
            for (int i = 0; i < inputPorts.Length; i++) {
                if (!graph.TryGetPortIndex(inputPorts[i], out int portIndex)) continue;

                PortSpec.FromEncoded(graph.PortTypes[portIndex], out var spec);
                if (spec.DataType == dataType) {
                    if (matchCount == localIndex) {
                        portId = inputPorts[i];
                        inputPorts.Dispose();
                        return true;
                    }
                    matchCount++;
                }
            }

            inputPorts.Dispose();
            return false;
        }

        [BurstCompile]
        public static bool TryGetOutputBySpec(
            in this Graph graph, uint nodeId, PortDataType dataType, int localIndex, out uint portId
        ) {
            portId = 0;
            graph.GetOutputPorts(nodeId, out var outputPorts, Allocator.Temp);

            int matchCount = 0;
            for (int i = 0; i < outputPorts.Length; i++) {
                if (!graph.TryGetPortIndex(outputPorts[i], out int portIndex)) continue;

                PortSpec.FromEncoded(graph.PortTypes[portIndex], out var spec);
                if (spec.DataType == dataType) {
                    if (matchCount == localIndex) {
                        portId = outputPorts[i];
                        outputPorts.Dispose();
                        return true;
                    }
                    matchCount++;
                }
            }

            outputPorts.Dispose();
            return false;
        }

        [BurstCompile]
        public static bool TryGetInput(
            in this Graph graph, uint nodeId, int index, out uint portId
        ) {
            portId = 0;
            graph.GetInputPorts(nodeId, out var inputPorts, Allocator.Temp);

            if (index < 0 || index >= inputPorts.Length) {
                inputPorts.Dispose();
                return false;
            }

            portId = inputPorts[index];
            inputPorts.Dispose();
            return true;
        }

        [BurstCompile]
        public static bool TryGetOutput(
            in this Graph graph, uint nodeId, int index, out uint portId
        ) {
            portId = 0;
            graph.GetOutputPorts(nodeId, out var outputPorts, Allocator.Temp);

            if (index < 0 || index >= outputPorts.Length) {
                outputPorts.Dispose();
                return false;
            }

            portId = outputPorts[index];
            outputPorts.Dispose();
            return true;
        }
    }
}
