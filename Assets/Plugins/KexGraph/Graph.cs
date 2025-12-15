using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexGraph {
    [BurstCompile]
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

        private NativeHashMap<uint, int> _nodeIndexMap;
        private NativeHashMap<uint, int> _portIndexMap;
        private NativeHashMap<uint, int> _edgeIndexMap;
        private uint _nextNodeId;
        private uint _nextPortId;
        private uint _nextEdgeId;

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

                _nodeIndexMap = new NativeHashMap<uint, int>(16, allocator),
                _portIndexMap = new NativeHashMap<uint, int>(16, allocator),
                _edgeIndexMap = new NativeHashMap<uint, int>(16, allocator),
                _nextNodeId = 1,
                _nextPortId = 1,
                _nextEdgeId = 1,
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

            if (_nodeIndexMap.IsCreated) _nodeIndexMap.Dispose();
            if (_portIndexMap.IsCreated) _portIndexMap.Dispose();
            if (_edgeIndexMap.IsCreated) _edgeIndexMap.Dispose();
        }

        public uint AddNode(uint nodeType, float2 position) {
            uint nodeId = _nextNodeId++;

            NodeIds.Add(nodeId);
            NodeTypes.Add(nodeType);
            NodePositions.Add(position);
            NodeInputCount.Add(0);
            NodeOutputCount.Add(0);

            _nodeIndexMap.Add(nodeId, NodeIds.Length - 1);

            return nodeId;
        }

        public readonly bool TryGetNodeIndex(uint nodeId, out int index) {
            if (_nodeIndexMap.TryGetValue(nodeId, out index)) {
                return true;
            }
            index = -1;
            return false;
        }

        public void RemoveNode(uint nodeId) {
            if (!TryGetNodeIndex(nodeId, out int index)) {
                return;
            }

            int lastIndex = NodeIds.Length - 1;

            if (index != lastIndex) {
                uint lastNodeId = NodeIds[lastIndex];

                NodeIds[index] = NodeIds[lastIndex];
                NodeTypes[index] = NodeTypes[lastIndex];
                NodePositions[index] = NodePositions[lastIndex];
                NodeInputCount[index] = NodeInputCount[lastIndex];
                NodeOutputCount[index] = NodeOutputCount[lastIndex];

                _nodeIndexMap[lastNodeId] = index;
            }

            NodeIds.RemoveAt(lastIndex);
            NodeTypes.RemoveAt(lastIndex);
            NodePositions.RemoveAt(lastIndex);
            NodeInputCount.RemoveAt(lastIndex);
            NodeOutputCount.RemoveAt(lastIndex);

            _nodeIndexMap.Remove(nodeId);
        }

        public readonly bool TryGetPortIndex(uint portId, out int index) {
            if (_portIndexMap.TryGetValue(portId, out index)) {
                return true;
            }
            index = -1;
            return false;
        }

        public uint AddInputPort(uint nodeId, uint portType) {
            if (!TryGetNodeIndex(nodeId, out int nodeIndex)) {
                return 0;
            }

            uint portId = _nextPortId++;

            PortIds.Add(portId);
            PortTypes.Add(portType);
            PortOwners.Add(nodeId);
            PortIsInput.Add(true);

            _portIndexMap.Add(portId, PortIds.Length - 1);
            NodeInputCount[nodeIndex]++;

            return portId;
        }

        public uint AddOutputPort(uint nodeId, uint portType) {
            if (!TryGetNodeIndex(nodeId, out int nodeIndex)) {
                return 0;
            }

            uint portId = _nextPortId++;

            PortIds.Add(portId);
            PortTypes.Add(portType);
            PortOwners.Add(nodeId);
            PortIsInput.Add(false);

            _portIndexMap.Add(portId, PortIds.Length - 1);
            NodeOutputCount[nodeIndex]++;

            return portId;
        }

        public readonly void GetInputPorts(uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            if (!TryGetNodeIndex(nodeId, out int nodeIndex)) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            int count = NodeInputCount[nodeIndex];
            if (count == 0) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            result = new NativeArray<uint>(count, allocator);
            int resultIndex = 0;

            for (int i = 0; i < PortIds.Length; i++) {
                if (PortOwners[i] == nodeId && PortIsInput[i]) {
                    result[resultIndex++] = PortIds[i];
                }
            }
        }

        public readonly void GetOutputPorts(uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            if (!TryGetNodeIndex(nodeId, out int nodeIndex)) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            int count = NodeOutputCount[nodeIndex];
            if (count == 0) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            result = new NativeArray<uint>(count, allocator);
            int resultIndex = 0;

            for (int i = 0; i < PortIds.Length; i++) {
                if (PortOwners[i] == nodeId && !PortIsInput[i]) {
                    result[resultIndex++] = PortIds[i];
                }
            }
        }

        public void RemovePort(uint portId) {
            if (!TryGetPortIndex(portId, out int index)) {
                return;
            }

            uint ownerId = PortOwners[index];
            bool isInput = PortIsInput[index];

            int lastIndex = PortIds.Length - 1;

            if (index != lastIndex) {
                uint lastPortId = PortIds[lastIndex];

                PortIds[index] = PortIds[lastIndex];
                PortTypes[index] = PortTypes[lastIndex];
                PortOwners[index] = PortOwners[lastIndex];
                PortIsInput[index] = PortIsInput[lastIndex];

                _portIndexMap[lastPortId] = index;
            }

            PortIds.RemoveAt(lastIndex);
            PortTypes.RemoveAt(lastIndex);
            PortOwners.RemoveAt(lastIndex);
            PortIsInput.RemoveAt(lastIndex);

            _portIndexMap.Remove(portId);

            if (TryGetNodeIndex(ownerId, out int nodeIndex)) {
                if (isInput) {
                    NodeInputCount[nodeIndex]--;
                } else {
                    NodeOutputCount[nodeIndex]--;
                }
            }
        }

        public readonly bool TryGetEdgeIndex(uint edgeId, out int index) {
            if (_edgeIndexMap.TryGetValue(edgeId, out index)) {
                return true;
            }
            index = -1;
            return false;
        }

        public uint AddEdge(uint sourcePortId, uint targetPortId) {
            if (!TryGetPortIndex(sourcePortId, out _)) {
                return 0;
            }
            if (!TryGetPortIndex(targetPortId, out _)) {
                return 0;
            }

            uint edgeId = _nextEdgeId++;

            EdgeIds.Add(edgeId);
            EdgeSources.Add(sourcePortId);
            EdgeTargets.Add(targetPortId);

            _edgeIndexMap.Add(edgeId, EdgeIds.Length - 1);

            return edgeId;
        }

        public void RemoveEdge(uint edgeId) {
            if (!TryGetEdgeIndex(edgeId, out int index)) {
                return;
            }

            int lastIndex = EdgeIds.Length - 1;

            if (index != lastIndex) {
                uint lastEdgeId = EdgeIds[lastIndex];

                EdgeIds[index] = EdgeIds[lastIndex];
                EdgeSources[index] = EdgeSources[lastIndex];
                EdgeTargets[index] = EdgeTargets[lastIndex];

                _edgeIndexMap[lastEdgeId] = index;
            }

            EdgeIds.RemoveAt(lastIndex);
            EdgeSources.RemoveAt(lastIndex);
            EdgeTargets.RemoveAt(lastIndex);

            _edgeIndexMap.Remove(edgeId);
        }

        public readonly void GetOutgoingEdges(uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            if (!TryGetNodeIndex(nodeId, out _)) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            GetOutputPorts(nodeId, out var outputPorts, Allocator.Temp);

            var tempList = new NativeList<uint>(EdgeCount, Allocator.Temp);

            for (int i = 0; i < EdgeIds.Length; i++) {
                for (int j = 0; j < outputPorts.Length; j++) {
                    if (EdgeSources[i] == outputPorts[j]) {
                        tempList.Add(EdgeIds[i]);
                        break;
                    }
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
            outputPorts.Dispose();
        }

        public readonly void GetIncomingEdges(uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            if (!TryGetNodeIndex(nodeId, out _)) {
                result = new NativeArray<uint>(0, allocator);
                return;
            }

            GetInputPorts(nodeId, out var inputPorts, Allocator.Temp);

            var tempList = new NativeList<uint>(EdgeCount, Allocator.Temp);

            for (int i = 0; i < EdgeIds.Length; i++) {
                for (int j = 0; j < inputPorts.Length; j++) {
                    if (EdgeTargets[i] == inputPorts[j]) {
                        tempList.Add(EdgeIds[i]);
                        break;
                    }
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
            inputPorts.Dispose();
        }

        public readonly void GetSuccessorNodes(uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            GetOutgoingEdges(nodeId, out var edges, Allocator.Temp);

            if (edges.Length == 0) {
                result = new NativeArray<uint>(0, allocator);
                edges.Dispose();
                return;
            }

            var tempList = new NativeList<uint>(edges.Length, Allocator.Temp);
            var seen = new NativeHashSet<uint>(edges.Length, Allocator.Temp);

            for (int i = 0; i < edges.Length; i++) {
                if (!TryGetEdgeIndex(edges[i], out int edgeIndex)) continue;

                uint targetPortId = EdgeTargets[edgeIndex];
                if (!TryGetPortIndex(targetPortId, out int portIndex)) continue;

                uint targetNodeId = PortOwners[portIndex];
                if (seen.Add(targetNodeId)) {
                    tempList.Add(targetNodeId);
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
            seen.Dispose();
            edges.Dispose();
        }

        public readonly void GetPredecessorNodes(uint nodeId, out NativeArray<uint> result, Allocator allocator) {
            GetIncomingEdges(nodeId, out var edges, Allocator.Temp);

            if (edges.Length == 0) {
                result = new NativeArray<uint>(0, allocator);
                edges.Dispose();
                return;
            }

            var tempList = new NativeList<uint>(edges.Length, Allocator.Temp);
            var seen = new NativeHashSet<uint>(edges.Length, Allocator.Temp);

            for (int i = 0; i < edges.Length; i++) {
                if (!TryGetEdgeIndex(edges[i], out int edgeIndex)) continue;

                uint sourcePortId = EdgeSources[edgeIndex];
                if (!TryGetPortIndex(sourcePortId, out int portIndex)) continue;

                uint sourceNodeId = PortOwners[portIndex];
                if (seen.Add(sourceNodeId)) {
                    tempList.Add(sourceNodeId);
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
            seen.Dispose();
            edges.Dispose();
        }

        public readonly void FindSourceNodes(out NativeArray<uint> result, Allocator allocator) {
            var tempList = new NativeList<uint>(NodeCount, Allocator.Temp);

            for (int i = 0; i < NodeIds.Length; i++) {
                uint nodeId = NodeIds[i];
                GetIncomingEdges(nodeId, out var incoming, Allocator.Temp);
                bool isSource = incoming.Length == 0;
                incoming.Dispose();

                if (isSource) {
                    tempList.Add(nodeId);
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
        }

        public readonly void FindSinkNodes(out NativeArray<uint> result, Allocator allocator) {
            var tempList = new NativeList<uint>(NodeCount, Allocator.Temp);

            for (int i = 0; i < NodeIds.Length; i++) {
                uint nodeId = NodeIds[i];
                GetOutgoingEdges(nodeId, out var outgoing, Allocator.Temp);
                bool isSink = outgoing.Length == 0;
                outgoing.Dispose();

                if (isSink) {
                    tempList.Add(nodeId);
                }
            }

            result = new NativeArray<uint>(tempList.AsArray(), allocator);

            tempList.Dispose();
        }

        public readonly bool HasCycle() {
            if (NodeCount == 0) return false;

            var visited = new NativeHashSet<uint>(NodeCount, Allocator.Temp);
            var inStack = new NativeHashSet<uint>(NodeCount, Allocator.Temp);
            var stack = new NativeList<long>(NodeCount, Allocator.Temp);

            bool cycleFound = false;

            for (int i = 0; i < NodeIds.Length && !cycleFound; i++) {
                uint startNode = NodeIds[i];
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

                    GetSuccessorNodes(nodeId, out var successors, Allocator.Temp);
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
