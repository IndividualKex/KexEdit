using KexEdit.Nodes;
using KexGraph;
using Unity.Burst;

namespace KexEdit.NodeGraph {
    [BurstCompile]
    public static class ConnectionValidatorExtensions {
        [BurstCompile]
        public static void ValidateConnection(
            in this Graph graph,
            uint sourcePortId,
            uint targetPortId,
            out ValidationResult result
        ) {
            if (!graph.TryGetPortIndex(sourcePortId, out int srcIdx)) {
                result = ValidationResult.Failure(ValidationError.SourcePortNotFound);
                return;
            }
            if (!graph.TryGetPortIndex(targetPortId, out int tgtIdx)) {
                result = ValidationResult.Failure(ValidationError.TargetPortNotFound);
                return;
            }

            if (graph.PortIsInput[srcIdx]) {
                result = ValidationResult.Failure(ValidationError.SourceMustBeOutput);
                return;
            }
            if (!graph.PortIsInput[tgtIdx]) {
                result = ValidationResult.Failure(ValidationError.TargetMustBeInput);
                return;
            }

            PortId srcType = (PortId)graph.PortTypes[srcIdx];
            PortId tgtType = (PortId)graph.PortTypes[tgtIdx];
            if (srcType != tgtType) {
                result = ValidationResult.Failure(ValidationError.IncompatiblePortTypes);
                return;
            }

            uint srcOwner = graph.PortOwners[srcIdx];
            uint tgtOwner = graph.PortOwners[tgtIdx];
            if (srcOwner == tgtOwner) {
                result = ValidationResult.Failure(ValidationError.SelfConnection);
                return;
            }

            result = ValidationResult.Success();
        }

        [BurstCompile]
        public static uint AddValidatedEdge(
            ref this Graph graph,
            uint sourcePortId,
            uint targetPortId,
            out ValidationResult result
        ) {
            graph.ValidateConnection(sourcePortId, targetPortId, out result);
            if (!result.IsValid) return 0;
            return graph.AddEdge(sourcePortId, targetPortId);
        }

        [BurstCompile]
        public static bool ValidateAllEdges(in this Graph graph, out int firstInvalidEdgeIndex) {
            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                uint src = graph.EdgeSources[i];
                uint tgt = graph.EdgeTargets[i];
                graph.ValidateConnection(src, tgt, out var result);
                if (!result.IsValid) {
                    firstInvalidEdgeIndex = i;
                    return false;
                }
            }
            firstInvalidEdgeIndex = -1;
            return true;
        }
    }
}
