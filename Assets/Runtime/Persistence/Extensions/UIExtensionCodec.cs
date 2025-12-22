using Unity.Collections;

namespace KexEdit.Persistence {
    public static class UIExtensionCodec {
        public static void Write(ref ChunkWriter writer, in UIStateChunk chunk) {
            writer.BeginChunk(ExtensionSchema.UIStateType, ExtensionSchema.UIStateVersion);

            writer.WriteInt(chunk.NodePositions.Count);
            foreach (var kvp in chunk.NodePositions) {
                writer.WriteUInt(kvp.Key);
                writer.WriteFloat2(kvp.Value);
            }

            writer.WriteFloat(chunk.TimelineOffset);
            writer.WriteFloat(chunk.TimelineZoom);
            writer.WriteFloat(chunk.PlayheadTime);
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

            writer.WriteInt(chunk.KeyframeStates.Length);
            for (int i = 0; i < chunk.KeyframeStates.Length; i++) {
                var state = chunk.KeyframeStates[i];
                writer.WriteUInt(state.NodeId);
                writer.WriteByte(state.PropertyId);
                writer.WriteInt(state.KeyframeIndex);
                writer.WriteUInt(state.Id);
                writer.WriteByte(state.HandleType);
                writer.WriteByte(state.Flags);
                writer.WriteByte(state.Selected);
            }

            writer.WriteHashSet(in chunk.SelectedNodeIds);
            writer.WriteHashSet(in chunk.SelectedConnectionIds);

            writer.EndChunk();
        }

        public static bool TryRead(ref ChunkReader reader, Allocator allocator, out UIStateChunk result) {
            result = UIStateChunk.Create(allocator);

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                if (header.TypeString == ExtensionSchema.UIStateType) {
                    int positionCount = reader.ReadInt();
                    for (int i = 0; i < positionCount; i++) {
                        uint nodeId = reader.ReadUInt();
                        var position = reader.ReadFloat2();
                        result.NodePositions[nodeId] = position;
                    }

                    result.TimelineOffset = reader.ReadFloat();
                    result.TimelineZoom = reader.ReadFloat();
                    result.PlayheadTime = reader.ReadFloat();
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

                    int keyframeCount = reader.ReadInt();
                    for (int i = 0; i < keyframeCount; i++) {
                        var state = new KeyframeUIState {
                            NodeId = reader.ReadUInt(),
                            PropertyId = reader.ReadByte(),
                            KeyframeIndex = reader.ReadInt(),
                            Id = reader.ReadUInt(),
                            HandleType = reader.ReadByte(),
                            Flags = reader.ReadByte(),
                            Selected = reader.ReadByte()
                        };
                        result.KeyframeStates.Add(state);
                    }

                    reader.ReadHashSet(ref result.SelectedNodeIds);
                    reader.ReadHashSet(ref result.SelectedConnectionIds);

                    return true;
                }

                reader.SkipChunk(header);
            }

            return false;
        }
    }
}
