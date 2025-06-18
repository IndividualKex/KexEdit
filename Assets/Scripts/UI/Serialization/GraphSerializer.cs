using Unity.Burst;
using Unity.Collections;

namespace KexEdit.UI.Serialization {
    [BurstCompile]
    public static class GraphSerializer {
        [BurstCompile]
        public static int Serialize(ref SerializedGraph graph, ref NativeArray<byte> buffer) {
            BinaryWriter writer = new(buffer);
            return SerializeBinary(ref graph, ref writer);
        }

        [BurstCompile]
        public static int Deserialize(ref SerializedGraph graph, ref NativeArray<byte> buffer) {
            BinaryReader reader = new(buffer);
            return DeserializeBinary(ref graph, ref reader);
        }

        [BurstCompile]
        private static int SerializeBinary(ref SerializedGraph graph, ref BinaryWriter writer) {
            int startPos = writer.Position;

            writer.Write(SerializationVersion.CURRENT);
            writer.Write(graph.Nodes.Length);

            for (int i = 0; i < graph.Nodes.Length; i++) {
                var node = graph.Nodes[i];
                SerializeNode(ref node, ref writer);
            }

            writer.WriteArray(graph.Edges);
            return writer.Position - startPos;
        }

        [BurstCompile]
        private static int DeserializeBinary(ref SerializedGraph graph, ref BinaryReader reader) {
            graph.Version = reader.Read<int>();
            int nodeCount = reader.Read<int>();
            graph.Nodes = new(nodeCount, Allocator.Temp);

            for (int i = 0; i < nodeCount; i++) {
                var node = new SerializedNode();
                DeserializeNode(ref node, ref reader, graph.Version);
                graph.Nodes[i] = node;
            }

            reader.ReadArray(out graph.Edges, Allocator.Temp);
            return reader.Position;
        }

        [BurstCompile]
        private static void SerializeNode(ref SerializedNode node, ref BinaryWriter writer) {
            writer.Write(node.Node);
            writer.Write(node.Anchor);
            writer.Write((uint)node.FieldFlags);

            // Write BooleanFlags if either Render or Selected flags are set
            if ((node.FieldFlags & (NodeFieldFlags.HasRender | NodeFieldFlags.HasSelected)) != 0) {
                writer.Write((byte)node.BooleanFlags);
            }

            // Write optional fields based on flags - add new fields here following this pattern
            if ((node.FieldFlags & NodeFieldFlags.HasPropertyOverrides) != 0) writer.Write(node.PropertyOverrides);
            if ((node.FieldFlags & NodeFieldFlags.HasSelectedProperties) != 0) writer.Write(node.SelectedProperties);
            if ((node.FieldFlags & NodeFieldFlags.HasCurveData) != 0) writer.Write(node.CurveData);
            if ((node.FieldFlags & NodeFieldFlags.HasDuration) != 0) writer.Write(node.Duration);

            writer.WriteArray(node.InputPorts);
            writer.WriteArray(node.OutputPorts);
            writer.WriteArray(node.RollSpeedKeyframes);
            writer.WriteArray(node.NormalForceKeyframes);
            writer.WriteArray(node.LateralForceKeyframes);
            writer.WriteArray(node.PitchSpeedKeyframes);
            writer.WriteArray(node.YawSpeedKeyframes);
            writer.WriteArray(node.FixedVelocityKeyframes);
            writer.WriteArray(node.HeartKeyframes);
            writer.WriteArray(node.FrictionKeyframes);
            writer.WriteArray(node.ResistanceKeyframes);
        }

        [BurstCompile]
        private static void DeserializeNode(ref SerializedNode node, ref BinaryReader reader, int version) {
            node.Node = reader.Read<Node>();
            node.Anchor = reader.Read<PointData>();

            if (version >= SerializationVersion.INITIAL) {
                uint flags = reader.Read<uint>();
                node.FieldFlags = (NodeFieldFlags)flags;

                // Read BooleanFlags if either Render or Selected flags are set
                if ((node.FieldFlags & (NodeFieldFlags.HasRender | NodeFieldFlags.HasSelected)) != 0) {
                    node.BooleanFlags = (NodeFlags)reader.Read<byte>();
                }

                // Read optional fields based on flags - add new fields here following this pattern
                node.PropertyOverrides = (node.FieldFlags & NodeFieldFlags.HasPropertyOverrides) != 0 ? reader.Read<PropertyOverrides>() : default;
                node.SelectedProperties = (node.FieldFlags & NodeFieldFlags.HasSelectedProperties) != 0 ? reader.Read<SelectedProperties>() : default;
                node.CurveData = (node.FieldFlags & NodeFieldFlags.HasCurveData) != 0 ? reader.Read<CurveData>() : default;
                node.Duration = (node.FieldFlags & NodeFieldFlags.HasDuration) != 0 ? reader.Read<Duration>() : default;
            }

            reader.ReadArray(out node.InputPorts, Allocator.Temp);
            reader.ReadArray(out node.OutputPorts, Allocator.Temp);
            reader.ReadArray(out node.RollSpeedKeyframes, Allocator.Temp);
            reader.ReadArray(out node.NormalForceKeyframes, Allocator.Temp);
            reader.ReadArray(out node.LateralForceKeyframes, Allocator.Temp);
            reader.ReadArray(out node.PitchSpeedKeyframes, Allocator.Temp);
            reader.ReadArray(out node.YawSpeedKeyframes, Allocator.Temp);
            reader.ReadArray(out node.FixedVelocityKeyframes, Allocator.Temp);
            reader.ReadArray(out node.HeartKeyframes, Allocator.Temp);
            reader.ReadArray(out node.FrictionKeyframes, Allocator.Temp);
            reader.ReadArray(out node.ResistanceKeyframes, Allocator.Temp);
        }
    }
}
