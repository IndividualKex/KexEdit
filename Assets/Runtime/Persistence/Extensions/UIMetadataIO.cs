using Unity.Collections;

namespace KexEdit.Persistence {
    public static class UIMetadataIO {
        public static void Write(ref ChunkWriter writer, in UIMetadataChunk chunk) {
            writer.WriteInt(chunk.Positions.Count);

            foreach (var kvp in chunk.Positions) {
                writer.WriteUInt(kvp.Key);
                writer.WriteFloat2(kvp.Value);
            }
        }

        public static UIMetadataChunk Read(ref ChunkReader reader, uint version, Allocator allocator) {
            var chunk = new UIMetadataChunk(allocator);

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                uint nodeId = reader.ReadUInt();
                var position = reader.ReadFloat2();
                chunk.Positions[nodeId] = position;
            }

            return chunk;
        }
    }
}
