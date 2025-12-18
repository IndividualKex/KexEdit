using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexGraph {
    public struct Graph : IDisposable {
        public NativeList<uint> NodeIds;
        public NativeList<uint> NodeTypes;
        public NativeList<float2> NodePositions;
        public NativeList<int> NodeInputCount;
        public NativeList<int> NodeOutputCount;

        public NativeList<uint> PortIds;
        public NativeList<uint> PortTypes;
        public NativeList<uint> PortOwners;
        public NativeList<bool> PortIsInput;

        public NativeList<uint> EdgeIds;
        public NativeList<uint> EdgeSources;
        public NativeList<uint> EdgeTargets;

        internal NativeHashMap<uint, int> NodeIndexMap;
        internal NativeHashMap<uint, int> PortIndexMap;
        internal NativeHashMap<uint, int> EdgeIndexMap;
        public uint NextNodeId;
        public uint NextPortId;
        public uint NextEdgeId;

        public readonly int NodeCount => NodeIds.IsCreated ? NodeIds.Length : 0;
        public readonly int PortCount => PortIds.IsCreated ? PortIds.Length : 0;
        public readonly int EdgeCount => EdgeIds.IsCreated ? EdgeIds.Length : 0;

        public static Graph Create(Allocator allocator) {
            return new Graph {
                NodeIds = new NativeList<uint>(allocator),
                NodeTypes = new NativeList<uint>(allocator),
                NodePositions = new NativeList<float2>(allocator),
                NodeInputCount = new NativeList<int>(allocator),
                NodeOutputCount = new NativeList<int>(allocator),

                PortIds = new NativeList<uint>(allocator),
                PortTypes = new NativeList<uint>(allocator),
                PortOwners = new NativeList<uint>(allocator),
                PortIsInput = new NativeList<bool>(allocator),

                EdgeIds = new NativeList<uint>(allocator),
                EdgeSources = new NativeList<uint>(allocator),
                EdgeTargets = new NativeList<uint>(allocator),

                NodeIndexMap = new NativeHashMap<uint, int>(16, allocator),
                PortIndexMap = new NativeHashMap<uint, int>(16, allocator),
                EdgeIndexMap = new NativeHashMap<uint, int>(16, allocator),
                NextNodeId = 1,
                NextPortId = 1,
                NextEdgeId = 1,
            };
        }

        public void Dispose() {
            if (NodeIds.IsCreated) NodeIds.Dispose();
            if (NodeTypes.IsCreated) NodeTypes.Dispose();
            if (NodePositions.IsCreated) NodePositions.Dispose();
            if (NodeInputCount.IsCreated) NodeInputCount.Dispose();
            if (NodeOutputCount.IsCreated) NodeOutputCount.Dispose();

            if (PortIds.IsCreated) PortIds.Dispose();
            if (PortTypes.IsCreated) PortTypes.Dispose();
            if (PortOwners.IsCreated) PortOwners.Dispose();
            if (PortIsInput.IsCreated) PortIsInput.Dispose();

            if (EdgeIds.IsCreated) EdgeIds.Dispose();
            if (EdgeSources.IsCreated) EdgeSources.Dispose();
            if (EdgeTargets.IsCreated) EdgeTargets.Dispose();

            if (NodeIndexMap.IsCreated) NodeIndexMap.Dispose();
            if (PortIndexMap.IsCreated) PortIndexMap.Dispose();
            if (EdgeIndexMap.IsCreated) EdgeIndexMap.Dispose();
        }
    }

    [BurstCompile]
    public static class GraphExtensions {
        [BurstCompile]
        public static int GetNodeCount(in this Graph graph) {
            return graph.NodeIds.IsCreated ? graph.NodeIds.Length : 0;
        }

        [BurstCompile]
        public static int GetPortCount(in this Graph graph) {
            return graph.PortIds.IsCreated ? graph.PortIds.Length : 0;
        }

        [BurstCompile]
        public static int GetEdgeCount(in this Graph graph) {
            return graph.EdgeIds.IsCreated ? graph.EdgeIds.Length : 0;
        }

        [BurstCompile]
        public static uint AddNode(ref this Graph graph, uint nodeType, in float2 position) {
            uint nodeId = graph.NextNodeId++;

            graph.NodeIds.Add(nodeId);
            graph.NodeTypes.Add(nodeType);
            graph.NodePositions.Add(position);
            graph.NodeInputCount.Add(0);
            graph.NodeOutputCount.Add(0);

            graph.NodeIndexMap.Add(nodeId, graph.NodeIds.Length - 1);

            return nodeId;
        }

        [BurstCompile]
        public static bool TryGetNodeIndex(in this Graph graph, uint nodeId, out int index) {
            if (graph.NodeIndexMap.TryGetValue(nodeId, out index)) {
                return true;
            }
            index = -1;
            return false;
        }

        [BurstCompile]
        public static void RemoveNode(ref this Graph graph, uint nodeId) {
            if (!graph.TryGetNodeIndex(nodeId, out int index)) {
                return;
            }

            int lastIndex = graph.NodeIds.Length - 1;

            if (index != lastIndex) {
                uint lastNodeId = graph.NodeIds[lastIndex];

                graph.NodeIds[index] = graph.NodeIds[lastIndex];
                graph.NodeTypes[index] = graph.NodeTypes[lastIndex];
                graph.NodePositions[index] = graph.NodePositions[lastIndex];
                graph.NodeInputCount[index] = graph.NodeInputCount[lastIndex];
                graph.NodeOutputCount[index] = graph.NodeOutputCount[lastIndex];

                graph.NodeIndexMap[lastNodeId] = index;
            }

            graph.NodeIds.RemoveAt(lastIndex);
            graph.NodeTypes.RemoveAt(lastIndex);
            graph.NodePositions.RemoveAt(lastIndex);
            graph.NodeInputCount.RemoveAt(lastIndex);
            graph.NodeOutputCount.RemoveAt(lastIndex);

            graph.NodeIndexMap.Remove(nodeId);
        }

        [BurstCompile]
        public static bool TryGetPortIndex(in this Graph graph, uint portId, out int index) {
            if (graph.PortIndexMap.TryGetValue(portId, out index)) {
                return true;
            }
            index = -1;
            return false;
        }

        [BurstCompile]
        public static uint AddInputPort(ref this Graph graph, uint nodeId, uint portType) {
            if (!graph.TryGetNodeIndex(nodeId, out int nodeIndex)) {
                return 0;
            }

            uint portId = graph.NextPortId++;

            graph.PortIds.Add(portId);
            graph.PortTypes.Add(portType);
            graph.PortOwners.Add(nodeId);
            graph.PortIsInput.Add(true);

            graph.PortIndexMap.Add(portId, graph.PortIds.Length - 1);
            graph.NodeInputCount[nodeIndex]++;

            return portId;
        }

        [BurstCompile]
        public static uint AddOutputPort(ref this Graph graph, uint nodeId, uint portType) {
            if (!graph.TryGetNodeIndex(nodeId, out int nodeIndex)) {
                return 0;
            }

            uint portId = graph.NextPortId++;

            graph.PortIds.Add(portId);
            graph.PortTypes.Add(portType);
            graph.PortOwners.Add(nodeId);
            graph.PortIsInput.Add(false);

            graph.PortIndexMap.Add(portId, graph.PortIds.Length - 1);
            graph.NodeOutputCount[nodeIndex]++;

            return portId;
        }

        [BurstCompile]
        public static void GetInputPorts(in this Graph graph, uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            if (!graph.TryGetNodeIndex(nodeId, out int nodeIndex)) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            int count = graph.NodeInputCount[nodeIndex];
            if (count == 0) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            result = new NativeArray<uint>(count, allocator);
            int resultIndex = 0;

            for (int i = 0; i < graph.PortIds.Length; i++) {
                if (graph.PortOwners[i] == nodeId && graph.PortIsInput[i]) {
                    result[resultIndex++] = graph.PortIds[i];
                }
            }
        }

        [BurstCompile]
        public static void GetOutputPorts(in this Graph graph, uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            if (!graph.TryGetNodeIndex(nodeId, out int nodeIndex)) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            int count = graph.NodeOutputCount[nodeIndex];
            if (count == 0) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            result = new NativeArray<uint>(count, allocator);
            int resultIndex = 0;

            for (int i = 0; i < graph.PortIds.Length; i++) {
                if (graph.PortOwners[i] == nodeId && !graph.PortIsInput[i]) {
                    result[resultIndex++] = graph.PortIds[i];
                }
            }
        }

        [BurstCompile]
        public static void RemovePort(ref this Graph graph, uint portId) {
            if (!graph.TryGetPortIndex(portId, out int index)) {
                return;
            }

            uint ownerId = graph.PortOwners[index];
            bool isInput = graph.PortIsInput[index];

            int lastIndex = graph.PortIds.Length - 1;

            if (index != lastIndex) {
                uint lastPortId = graph.PortIds[lastIndex];

                graph.PortIds[index] = graph.PortIds[lastIndex];
                graph.PortTypes[index] = graph.PortTypes[lastIndex];
                graph.PortOwners[index] = graph.PortOwners[lastIndex];
                graph.PortIsInput[index] = graph.PortIsInput[lastIndex];

                graph.PortIndexMap[lastPortId] = index;
            }

            graph.PortIds.RemoveAt(lastIndex);
            graph.PortTypes.RemoveAt(lastIndex);
            graph.PortOwners.RemoveAt(lastIndex);
            graph.PortIsInput.RemoveAt(lastIndex);

            graph.PortIndexMap.Remove(portId);

            if (graph.TryGetNodeIndex(ownerId, out int nodeIndex)) {
                if (isInput) {
                    graph.NodeInputCount[nodeIndex]--;
                } else {
                    graph.NodeOutputCount[nodeIndex]--;
                }
            }
        }

        [BurstCompile]
        public static bool TryGetEdgeIndex(in this Graph graph, uint edgeId, out int index) {
            if (graph.EdgeIndexMap.TryGetValue(edgeId, out index)) {
                return true;
            }
            index = -1;
            return false;
        }

        [BurstCompile]
        public static uint AddEdge(ref this Graph graph, uint sourcePortId, uint targetPortId) {
            if (!graph.TryGetPortIndex(sourcePortId, out _)) {
                return 0;
            }
            if (!graph.TryGetPortIndex(targetPortId, out _)) {
                return 0;
            }

            uint edgeId = graph.NextEdgeId++;

            graph.EdgeIds.Add(edgeId);
            graph.EdgeSources.Add(sourcePortId);
            graph.EdgeTargets.Add(targetPortId);

            graph.EdgeIndexMap.Add(edgeId, graph.EdgeIds.Length - 1);

            return edgeId;
        }

        [BurstCompile]
        public static void RemoveEdge(ref this Graph graph, uint edgeId) {
            if (!graph.TryGetEdgeIndex(edgeId, out int index)) {
                return;
            }

            int lastIndex = graph.EdgeIds.Length - 1;

            if (index != lastIndex) {
                uint lastEdgeId = graph.EdgeIds[lastIndex];

                graph.EdgeIds[index] = graph.EdgeIds[lastIndex];
                graph.EdgeSources[index] = graph.EdgeSources[lastIndex];
                graph.EdgeTargets[index] = graph.EdgeTargets[lastIndex];

                graph.EdgeIndexMap[lastEdgeId] = index;
            }

            graph.EdgeIds.RemoveAt(lastIndex);
            graph.EdgeSources.RemoveAt(lastIndex);
            graph.EdgeTargets.RemoveAt(lastIndex);

            graph.EdgeIndexMap.Remove(edgeId);
        }

        [BurstCompile]
        public static void GetOutgoingEdges(in this Graph graph, uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            if (!graph.TryGetNodeIndex(nodeId, out _)) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            graph.GetOutputPorts(nodeId, out var outputPorts, Allocator.Temp);

            int edgeCount = graph.GetEdgeCount();
            var tempList = new NativeList<uint>(edgeCount, Allocator.Temp);

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                for (int j = 0; j < outputPorts.Length; j++) {
                    if (graph.EdgeSources[i] == outputPorts[j]) {
                        tempList.Add(graph.EdgeIds[i]);
                        break;
                    }
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
            outputPorts.Dispose();
        }

        [BurstCompile]
        public static void GetIncomingEdges(in this Graph graph, uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            if (!graph.TryGetNodeIndex(nodeId, out _)) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            graph.GetInputPorts(nodeId, out var inputPorts, Allocator.Temp);

            int edgeCount = graph.GetEdgeCount();
            var tempList = new NativeList<uint>(edgeCount, Allocator.Temp);

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                for (int j = 0; j < inputPorts.Length; j++) {
                    if (graph.EdgeTargets[i] == inputPorts[j]) {
                        tempList.Add(graph.EdgeIds[i]);
                        break;
                    }
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
            inputPorts.Dispose();
        }

        [BurstCompile]
        public static void GetSuccessorNodes(in this Graph graph, uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            graph.GetOutgoingEdges(nodeId, out var edges, Allocator.Temp);

            if (edges.Length == 0) {
                result = new NativeArray<uint>(0, allocator);
                edges.Dispose();
                return;
            }

            var tempList = new NativeList<uint>(edges.Length, Allocator.Temp);
            var seen = new NativeHashSet<uint>(edges.Length, Allocator.Temp);

            for (int i = 0; i < edges.Length; i++) {
                if (!graph.TryGetEdgeIndex(edges[i], out int edgeIndex)) continue;

                uint targetPortId = graph.EdgeTargets[edgeIndex];
                if (!graph.TryGetPortIndex(targetPortId, out int portIndex)) continue;

                uint targetNodeId = graph.PortOwners[portIndex];
                if (seen.Add(targetNodeId)) {
                    tempList.Add(targetNodeId);
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
            seen.Dispose();
            edges.Dispose();
        }

        [BurstCompile]
        public static void GetPredecessorNodes(in this Graph graph, uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            graph.GetIncomingEdges(nodeId, out var edges, Allocator.Temp);

            if (edges.Length == 0) {
                result = new NativeArray<uint>(0, allocator);
                edges.Dispose();
                return;
            }

            var tempList = new NativeList<uint>(edges.Length, Allocator.Temp);
            var seen = new NativeHashSet<uint>(edges.Length, Allocator.Temp);

            for (int i = 0; i < edges.Length; i++) {
                if (!graph.TryGetEdgeIndex(edges[i], out int edgeIndex)) continue;

                uint sourcePortId = graph.EdgeSources[edgeIndex];
                if (!graph.TryGetPortIndex(sourcePortId, out int portIndex)) continue;

                uint sourceNodeId = graph.PortOwners[portIndex];
                if (seen.Add(sourceNodeId)) {
                    tempList.Add(sourceNodeId);
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
            seen.Dispose();
            edges.Dispose();
        }

        [BurstCompile]
        public static void FindSourceNodes(in this Graph graph, out NativeArray<uint> result, Allocator allocator) {
            int nodeCount = graph.GetNodeCount();
            var tempList = new NativeList<uint>(nodeCount, Allocator.Temp);

            for (int i = 0; i < graph.NodeIds.Length; i++) {
                uint nodeId = graph.NodeIds[i];
                graph.GetIncomingEdges(nodeId, out var incoming, Allocator.Temp);
                bool isSource = incoming.Length == 0;
                incoming.Dispose();

                if (isSource) {
                    tempList.Add(nodeId);
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
        }

        [BurstCompile]
        public static void FindSinkNodes(in this Graph graph, out NativeArray<uint> result, Allocator allocator) {
            int nodeCount = graph.GetNodeCount();
            var tempList = new NativeList<uint>(nodeCount, Allocator.Temp);

            for (int i = 0; i < graph.NodeIds.Length; i++) {
                uint nodeId = graph.NodeIds[i];
                graph.GetOutgoingEdges(nodeId, out var outgoing, Allocator.Temp);
                bool isSink = outgoing.Length == 0;
                outgoing.Dispose();

                if (isSink) {
                    tempList.Add(nodeId);
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
        }

        [BurstCompile]
        public static bool HasCycle(in this Graph graph) {
            int nodeCount = graph.GetNodeCount();
            if (nodeCount == 0) return false;

            var visited = new NativeHashSet<uint>(nodeCount, Allocator.Temp);
            var inStack = new NativeHashSet<uint>(nodeCount, Allocator.Temp);
            var stack = new NativeList<long>(nodeCount, Allocator.Temp);

            bool cycleFound = false;

            for (int i = 0; i < graph.NodeIds.Length && !cycleFound; i++) {
                uint startNode = graph.NodeIds[i];
                if (visited.Contains(startNode)) continue;

                stack.Add(startNode);

                while (stack.Length > 0 && !cycleFound) {
                    long item = stack[^1];
                    stack.RemoveAt(stack.Length - 1);

                    if (item < 0) {
                        inStack.Remove((uint)(~item));
                        continue;
                    }

                    uint nodeId = (uint)item;

                    if (visited.Contains(nodeId)) continue;

                    visited.Add(nodeId);
                    inStack.Add(nodeId);
                    stack.Add(~(long)nodeId);

                    graph.GetSuccessorNodes(nodeId, out var successors, Allocator.Temp);
                    for (int j = 0; j < successors.Length; j++) {
                        uint succ = successors[j];
                        if (inStack.Contains(succ)) {
                            cycleFound = true;
                            break;
                        }
                        if (!visited.Contains(succ)) {
                            stack.Add(succ);
                        }
                    }
                    successors.Dispose();
                }
            }

            stack.Dispose();
            visited.Dispose();
            inStack.Dispose();

            return cycleFound;
        }
    }
}
