namespace KexEdit.Persistence {
    public static class KeyframeUICodec {
        public static void WriteChunk(ref ChunkWriter writer, in KeyframeUIChunk chunk) {
            writer.BeginChunk(ExtensionSchema.KeyframeUIType, ExtensionSchema.KeyframeUIVersion);

            writer.WriteInt(chunk.States.Length);
            for (int i = 0; i < chunk.States.Length; i++) {
                var state = chunk.States[i];
                writer.WriteUInt(state.NodeId);
                writer.WriteByte(state.PropertyId);
                writer.WriteInt(state.KeyframeIndex);
                writer.WriteUInt(state.Id);
                writer.WriteByte(state.HandleType);
                writer.WriteByte(state.Flags);
            }

            writer.EndChunk();
        }

        public static bool TryReadFromFile(ref ChunkReader reader, out KeyframeUIChunk result) {
            result = KeyframeUIChunk.Create(Unity.Collections.Allocator.Persistent);

            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadUInt();

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                if (header.TypeString == ExtensionSchema.KeyframeUIType) {
                    int count = reader.ReadInt();
                    for (int i = 0; i < count; i++) {
                        var state = new KeyframeUIState {
                            NodeId = reader.ReadUInt(),
                            PropertyId = reader.ReadByte(),
                            KeyframeIndex = reader.ReadInt(),
                            Id = reader.ReadUInt(),
                            HandleType = reader.ReadByte(),
                            Flags = reader.ReadByte()
                        };
                        result.States.Add(state);
                    }
                    return true;
                }

                reader.SkipChunk(header);
            }

            return false;
        }
    }
}
