using KexEdit.Graph;
using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Persistence {
    [BurstCompile]
    public static class GraphCodec {
        public const uint Version = 1;

        public static void Write(ref ChunkWriter writer, in KexEdit.Graph.Graph graph) {
            writer.WriteInt(graph.NodeIds.Length);
            writer.WriteInt(graph.PortIds.Length);
            writer.WriteInt(graph.EdgeIds.Length);

            for (int i = 0; i < graph.NodeIds.Length; i++) {
                writer.WriteUInt(graph.NodeIds[i]);
                writer.WriteUInt(graph.NodeTypes[i]);
                writer.WriteInt(graph.NodeInputCount[i]);
                writer.WriteInt(graph.NodeOutputCount[i]);
            }

            for (int i = 0; i < graph.PortIds.Length; i++) {
                writer.WriteUInt(graph.PortIds[i]);
                writer.WriteUInt(graph.PortTypes[i]);
                writer.WriteUInt(graph.PortOwners[i]);
                writer.WriteBool(graph.PortIsInput[i]);
            }

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                writer.WriteUInt(graph.EdgeIds[i]);
                writer.WriteUInt(graph.EdgeSources[i]);
                writer.WriteUInt(graph.EdgeTargets[i]);
            }

            writer.WriteUInt(graph.NextNodeId);
            writer.WriteUInt(graph.NextPortId);
            writer.WriteUInt(graph.NextEdgeId);
        }

        public static void Read(ref ChunkReader reader, ref KexEdit.Graph.Graph graph) {
            int nodeCount = reader.ReadInt();
            int portCount = reader.ReadInt();
            int edgeCount = reader.ReadInt();

            for (int i = 0; i < nodeCount; i++) {
                uint id = reader.ReadUInt();
                uint type = reader.ReadUInt();
                int inputCount = reader.ReadInt();
                int outputCount = reader.ReadInt();

                graph.NodeIds.Add(id);
                graph.NodeTypes.Add(type);
                graph.NodeInputCount.Add(inputCount);
                graph.NodeOutputCount.Add(outputCount);
                graph.NodePositions.Add(float2.zero);
            }

            for (int i = 0; i < portCount; i++) {
                uint id = reader.ReadUInt();
                uint type = reader.ReadUInt();
                uint owner = reader.ReadUInt();
                bool isInput = reader.ReadBool();

                graph.PortIds.Add(id);
                graph.PortTypes.Add(type);
                graph.PortOwners.Add(owner);
                graph.PortIsInput.Add(isInput);
            }

            for (int i = 0; i < edgeCount; i++) {
                uint id = reader.ReadUInt();
                uint source = reader.ReadUInt();
                uint target = reader.ReadUInt();

                graph.EdgeIds.Add(id);
                graph.EdgeSources.Add(source);
                graph.EdgeTargets.Add(target);
            }

            graph.NextNodeId = reader.ReadUInt();
            graph.NextPortId = reader.ReadUInt();
            graph.NextEdgeId = reader.ReadUInt();

            graph.RebuildIndexMaps();
        }
    }
}
