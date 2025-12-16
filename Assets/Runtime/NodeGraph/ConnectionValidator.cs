using KexEdit.Nodes;
using KexGraph;
using Unity.Burst;

namespace KexEdit.NodeGraph {
    [BurstCompile]
    public static class ConnectionValidatorExtensions {
        [BurstCompile]
        public static bool ValidateConnection(
            in this Graph graph,
            uint sourcePortId,
            uint targetPortId,
            out ValidationError error
        ) {
            if (!graph.TryGetPortIndex(sourcePortId, out int srcIdx)) {
                error = ValidationError.SourcePortNotFound;
                return false;
            }
            if (!graph.TryGetPortIndex(targetPortId, out int tgtIdx)) {
                error = ValidationError.TargetPortNotFound;
                return false;
            }

            if (graph.PortIsInput[srcIdx]) {
                error = ValidationError.SourceMustBeOutput;
                return false;
            }
            if (!graph.PortIsInput[tgtIdx]) {
                error = ValidationError.TargetMustBeInput;
                return false;
            }

            PortId srcType = (PortId)graph.PortTypes[srcIdx];
            PortId tgtType = (PortId)graph.PortTypes[tgtIdx];
            if (srcType.DataType() != tgtType.DataType()) {
                error = ValidationError.IncompatiblePortTypes;
                return false;
            }

            uint srcOwner = graph.PortOwners[srcIdx];
            uint tgtOwner = graph.PortOwners[tgtIdx];
            if (srcOwner == tgtOwner) {
                error = ValidationError.SelfConnection;
                return false;
            }

            error = ValidationError.None;
            return true;
        }

        [BurstCompile]
        public static uint AddValidatedEdge(
            ref this Graph graph,
            uint sourcePortId,
            uint targetPortId,
            out ValidationError error
        ) {
            if (!graph.ValidateConnection(sourcePortId, targetPortId, out error)) {
                return 0;
            }
            return graph.AddEdge(sourcePortId, targetPortId);
        }

        [BurstCompile]
        public static bool ValidateAllEdges(in this Graph graph, out int firstInvalidEdgeIndex) {
            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                uint src = graph.EdgeSources[i];
                uint tgt = graph.EdgeTargets[i];
                if (!graph.ValidateConnection(src, tgt, out _)) {
                    firstInvalidEdgeIndex = i;
                    return false;
                }
            }
            firstInvalidEdgeIndex = -1;
            return true;
        }
    }
}
