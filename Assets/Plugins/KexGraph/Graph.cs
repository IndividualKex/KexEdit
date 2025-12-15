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
                _nextNodeId = 1,
                _nextPortId = 1,
                _nextEdgeId = 1,
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
    }
}
