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
        public NativeList<int> NodeInputStart;
        public NativeList<int> NodeInputCount;
        public NativeList<int> NodeOutputStart;
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
        private uint _nextNodeId;
        private uint _nextPortId;

        public readonly int NodeCount => NodeIds.IsCreated ? NodeIds.Length : 0;
        public readonly int PortCount => PortIds.IsCreated ? PortIds.Length : 0;
        public readonly int EdgeCount => EdgeIds.IsCreated ? EdgeIds.Length : 0;

        public static Graph Create(Allocator allocator) {
            return new Graph {
                NodeIds = new NativeList<uint>(allocator),
                NodeTypes = new NativeList<uint>(allocator),
                NodePositions = new NativeList<float2>(allocator),
                NodeInputStart = new NativeList<int>(allocator),
                NodeInputCount = new NativeList<int>(allocator),
                NodeOutputStart = new NativeList<int>(allocator),
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
                _nextNodeId = 1,
                _nextPortId = 1,
            };
        }

        public void Dispose() {
            if (NodeIds.IsCreated) NodeIds.Dispose();
            if (NodeTypes.IsCreated) NodeTypes.Dispose();
            if (NodePositions.IsCreated) NodePositions.Dispose();
            if (NodeInputStart.IsCreated) NodeInputStart.Dispose();
            if (NodeInputCount.IsCreated) NodeInputCount.Dispose();
            if (NodeOutputStart.IsCreated) NodeOutputStart.Dispose();
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
        }

        public uint AddNode(uint nodeType, float2 position) {
            uint nodeId = _nextNodeId++;

            NodeIds.Add(nodeId);
            NodeTypes.Add(nodeType);
            NodePositions.Add(position);
            NodeInputStart.Add(0);
            NodeInputCount.Add(0);
            NodeOutputStart.Add(0);
            NodeOutputCount.Add(0);

            _nodeIndexMap.Add(nodeId, NodeIds.Length - 1);

            return nodeId;
        }

        public bool TryGetNodeIndex(uint nodeId, out int index) {
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
                NodeInputStart[index] = NodeInputStart[lastIndex];
                NodeInputCount[index] = NodeInputCount[lastIndex];
                NodeOutputStart[index] = NodeOutputStart[lastIndex];
                NodeOutputCount[index] = NodeOutputCount[lastIndex];

                _nodeIndexMap[lastNodeId] = index;
            }

            NodeIds.RemoveAt(lastIndex);
            NodeTypes.RemoveAt(lastIndex);
            NodePositions.RemoveAt(lastIndex);
            NodeInputStart.RemoveAt(lastIndex);
            NodeInputCount.RemoveAt(lastIndex);
            NodeOutputStart.RemoveAt(lastIndex);
            NodeOutputCount.RemoveAt(lastIndex);

            _nodeIndexMap.Remove(nodeId);
        }

        public bool TryGetPortIndex(uint portId, out int index) {
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

            int currentCount = NodeInputCount[nodeIndex];
            if (currentCount == 0) {
                NodeInputStart[nodeIndex] = PortIds.Length - 1;
            }
            NodeInputCount[nodeIndex] = currentCount + 1;

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

            int currentCount = NodeOutputCount[nodeIndex];
            if (currentCount == 0) {
                NodeOutputStart[nodeIndex] = PortIds.Length - 1;
            }
            NodeOutputCount[nodeIndex] = currentCount + 1;

            return portId;
        }

        public void GetInputPorts(uint nodeId, out NativeArray<uint> result, Allocator allocator) {
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

        public void GetOutputPorts(uint nodeId, out NativeArray<uint> result, Allocator allocator) {
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
    }
}
