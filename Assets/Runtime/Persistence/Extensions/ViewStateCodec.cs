namespace KexEdit.Persistence {
    public static class ViewStateCodec {
        public static void WriteChunk(ref ChunkWriter writer, in ViewStateChunk chunk) {
            writer.BeginChunk(ExtensionSchema.ViewStateType, ExtensionSchema.ViewStateVersion);

            writer.WriteFloat(chunk.TimelineOffset);
            writer.WriteFloat(chunk.TimelineZoom);
            writer.WriteFloat(chunk.GraphPanX);
            writer.WriteFloat(chunk.GraphPanY);
            writer.WriteFloat(chunk.GraphZoom);
            writer.WriteFloat3(chunk.CameraPosition);
            writer.WriteFloat3(chunk.CameraTargetPosition);
            writer.WriteFloat(chunk.CameraDistance);
            writer.WriteFloat(chunk.CameraTargetDistance);
            writer.WriteFloat(chunk.CameraPitch);
            writer.WriteFloat(chunk.CameraTargetPitch);
            writer.WriteFloat(chunk.CameraYaw);
            writer.WriteFloat(chunk.CameraTargetYaw);
            writer.WriteFloat(chunk.CameraSpeedMultiplier);

            writer.EndChunk();
        }

        public static bool TryReadFromFile(ref ChunkReader reader, out ViewStateChunk result) {
            result = ViewStateChunk.Default;

            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadUInt();

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                if (header.TypeString == ExtensionSchema.ViewStateType) {
                    result.TimelineOffset = reader.ReadFloat();
                    result.TimelineZoom = reader.ReadFloat();
                    result.GraphPanX = reader.ReadFloat();
                    result.GraphPanY = reader.ReadFloat();
                    result.GraphZoom = reader.ReadFloat();
                    result.CameraPosition = reader.ReadFloat3();
                    result.CameraTargetPosition = reader.ReadFloat3();
                    result.CameraDistance = reader.ReadFloat();
                    result.CameraTargetDistance = reader.ReadFloat();
                    result.CameraPitch = reader.ReadFloat();
                    result.CameraTargetPitch = reader.ReadFloat();
                    result.CameraYaw = reader.ReadFloat();
                    result.CameraTargetYaw = reader.ReadFloat();
                    result.CameraSpeedMultiplier = reader.ReadFloat();
                    return true;
                }

                reader.SkipChunk(header);
            }

            return false;
        }
    }
}
