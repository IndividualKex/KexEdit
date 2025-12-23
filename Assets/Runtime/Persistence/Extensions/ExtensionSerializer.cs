using Unity.Collections;

namespace KexEdit.Persistence {
    public static class ExtensionSerializer {
        public static void WriteUIMetadata(ref ChunkWriter writer, in UIMetadataChunk chunk) {
            writer.BeginChunk(ExtensionSchema.UIMetadataType, ExtensionSchema.UIMetadataVersion);
            UIMetadataIO.Write(ref writer, in chunk);
            writer.EndChunk();
        }

        public static ExtensionData ReadExtensions(ref ChunkReader reader, Allocator allocator) {
            var data = ExtensionData.Create(allocator);

            // Skip file header
            reader.ReadByte(); // K
            reader.ReadByte(); // E
            reader.ReadByte(); // X
            reader.ReadByte(); // D
            reader.ReadUInt(); // version

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                switch (header.TypeString) {
                    case "CORE":
                        reader.SkipChunk(header);
                        break;

                    case ExtensionSchema.UIMetadataType:
                        data.UIMetadata = UIMetadataIO.Read(ref reader, header.Version, allocator);
                        data.HasUIMetadata = true;
                        break;

                    default:
                        reader.SkipChunk(header);
                        break;
                }
            }

            return data;
        }
    }
}
