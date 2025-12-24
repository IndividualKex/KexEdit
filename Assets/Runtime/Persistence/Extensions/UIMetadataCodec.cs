using Unity.Collections;

namespace KexEdit.Persistence {
    public static class UIMetadataCodec {
        public static void WriteChunk(ref ChunkWriter writer, in UIMetadataChunk chunk) {
            writer.BeginChunk(ExtensionSchema.UIMetadataType, ExtensionSchema.UIMetadataVersion);
            writer.WriteInt(chunk.Positions.Count);
            foreach (var kvp in chunk.Positions) {
                writer.WriteUInt(kvp.Key);
                writer.WriteFloat2(kvp.Value);
            }
            writer.EndChunk();
        }

        public static bool TryReadFromFile(ref ChunkReader reader, Allocator allocator, out UIMetadataChunk result) {
            result = new UIMetadataChunk(allocator);

            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadUInt();

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                if (header.TypeString == ExtensionSchema.UIMetadataType) {
                    int count = reader.ReadInt();
                    for (int i = 0; i < count; i++) {
                        uint nodeId = reader.ReadUInt();
                        var position = reader.ReadFloat2();
                        result.Positions[nodeId] = position;
                    }
                    return true;
                }

                reader.SkipChunk(header);
            }

            return false;
        }
    }
}
