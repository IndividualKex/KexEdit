using Unity.Burst;
using Unity.Collections;

namespace KexGraph {
    [BurstCompile]
    public static class GraphSerializationExtensions {
        [BurstCompile]
        public static void RebuildIndexMaps(ref this Graph graph) {
            graph.NodeIndexMap.Clear();
            graph.PortIndexMap.Clear();
            graph.EdgeIndexMap.Clear();

            if (graph.NodeIds.Length > 0) {
                graph.NodeIndexMap.Capacity = graph.NodeIds.Length;
            }
            if (graph.PortIds.Length > 0) {
                graph.PortIndexMap.Capacity = graph.PortIds.Length;
            }
            if (graph.EdgeIds.Length > 0) {
                graph.EdgeIndexMap.Capacity = graph.EdgeIds.Length;
            }

            for (int i = 0; i < graph.NodeIds.Length; i++) {
                graph.NodeIndexMap.Add(graph.NodeIds[i], i);
            }

            for (int i = 0; i < graph.PortIds.Length; i++) {
                graph.PortIndexMap.Add(graph.PortIds[i], i);
            }

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                graph.EdgeIndexMap.Add(graph.EdgeIds[i], i);
            }
        }
    }
}
