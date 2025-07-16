using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.UI.Serialization {
    [System.Serializable]
    internal struct KeyframeV1 {
#pragma warning disable 0649
        public uint Id;
        public float Time;
        public float Value;
        public InterpolationType InInterpolation;
        public InterpolationType OutInterpolation;
        public float InTangent;
        public float OutTangent;
        public float InWeight;
        public float OutWeight;
        public bool Selected;
#pragma warning restore 0649

        public Keyframe ToCurrentKeyframe() {
            var handleType = HandleType.Free;
            if ((int)InInterpolation == 3 || (int)OutInterpolation == 3) {
                handleType = HandleType.Aligned;
            }

            return new Keyframe {
                Id = Id,
                Time = Time,
                Value = Value,
                InInterpolation = (int)InInterpolation == 3 ? InterpolationType.Bezier : InInterpolation,
                OutInterpolation = (int)OutInterpolation == 3 ? InterpolationType.Bezier : OutInterpolation,
                HandleType = handleType,
                Flags = KeyframeFlags.None,
                InTangent = InTangent,
                OutTangent = OutTangent,
                InWeight = InWeight,
                OutWeight = OutWeight,
                Selected = Selected
            };
        }
    }
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

            writer.Write(graph.UIState.TimelineOffset);
            writer.Write(graph.UIState.TimelineZoom);
            writer.Write(graph.UIState.NodeGraphPanX);
            writer.Write(graph.UIState.NodeGraphPanY);
            writer.Write(graph.UIState.NodeGraphZoom);
            writer.Write(graph.UIState.CameraTargetPositionX);
            writer.Write(graph.UIState.CameraTargetPositionY);
            writer.Write(graph.UIState.CameraTargetPositionZ);
            writer.Write(graph.UIState.CameraTargetDistance);
            writer.Write(graph.UIState.CameraTargetPitch);
            writer.Write(graph.UIState.CameraTargetYaw);
            writer.Write(graph.UIState.CameraSpeedMultiplier);

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
            int fileVersion = reader.Read<int>();

            if (fileVersion >= SerializationVersion.UI_STATE_SERIALIZATION) {
                graph.UIState.TimelineOffset = reader.Read<float>();
                graph.UIState.TimelineZoom = reader.Read<float>();
                graph.UIState.NodeGraphPanX = reader.Read<float>();
                graph.UIState.NodeGraphPanY = reader.Read<float>();
                graph.UIState.NodeGraphZoom = reader.Read<float>();
                graph.UIState.CameraTargetPositionX = reader.Read<float>();
                graph.UIState.CameraTargetPositionY = reader.Read<float>();
                graph.UIState.CameraTargetPositionZ = reader.Read<float>();
                graph.UIState.CameraTargetDistance = reader.Read<float>();
                graph.UIState.CameraTargetPitch = reader.Read<float>();
                graph.UIState.CameraTargetYaw = reader.Read<float>();
                graph.UIState.CameraSpeedMultiplier = reader.Read<float>();
            }
            else {
                float3 defaultPosition = new(6f, 6f, 6f);
                float3 defaultEuler = new(30f, -135f, 0f);
                graph.UIState = new SerializedUIState {
                    TimelineOffset = 0f,
                    TimelineZoom = 1f,
                    NodeGraphPanX = 0f,
                    NodeGraphPanY = 0f,
                    NodeGraphZoom = 1f,
                    CameraPositionX = defaultPosition.x,
                    CameraPositionY = defaultPosition.y,
                    CameraPositionZ = defaultPosition.z,
                    CameraTargetPositionX = defaultPosition.x,
                    CameraTargetPositionY = defaultPosition.y,
                    CameraTargetPositionZ = defaultPosition.z,
                    CameraDistance = math.length(defaultPosition),
                    CameraTargetDistance = math.length(defaultPosition),
                    CameraPitch = defaultEuler.x,
                    CameraTargetPitch = defaultEuler.x,
                    CameraYaw = defaultEuler.y,
                    CameraTargetYaw = defaultEuler.y,
                    CameraSpeedMultiplier = 1f
                };
            }

            int nodeCount = reader.Read<int>();
            graph.Nodes = new(nodeCount, Allocator.Temp);

            for (int i = 0; i < nodeCount; i++) {
                var node = new SerializedNode();
                DeserializeNode(ref node, ref reader, fileVersion);
                graph.Nodes[i] = node;
            }

            reader.ReadArray(out graph.Edges, Allocator.Temp);

            graph.Version = fileVersion < SerializationVersion.CURRENT ? SerializationVersion.CURRENT : fileVersion;
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
            if ((node.FieldFlags & NodeFieldFlags.HasMeshFilePath) != 0) writer.Write(node.MeshFilePath);

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
                node.MeshFilePath = (node.FieldFlags & NodeFieldFlags.HasMeshFilePath) != 0 ? reader.Read<FixedString512Bytes>() : default;
            }

            reader.ReadArray(out node.InputPorts, Allocator.Temp);
            reader.ReadArray(out node.OutputPorts, Allocator.Temp);

            if (version < SerializationVersion.PRECISION_MIGRATION) {
                ReadLegacyKeyframes(ref node, ref reader);
            }
            else {
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

        [BurstCompile]
        private static void ReadLegacyKeyframes(ref SerializedNode node, ref BinaryReader reader) {
            ReadLegacyKeyframeArray(out node.RollSpeedKeyframes, ref reader);
            ReadLegacyKeyframeArray(out node.NormalForceKeyframes, ref reader);
            ReadLegacyKeyframeArray(out node.LateralForceKeyframes, ref reader);
            ReadLegacyKeyframeArray(out node.PitchSpeedKeyframes, ref reader);
            ReadLegacyKeyframeArray(out node.YawSpeedKeyframes, ref reader);
            ReadLegacyKeyframeArray(out node.FixedVelocityKeyframes, ref reader);
            ReadLegacyKeyframeArray(out node.HeartKeyframes, ref reader);
            ReadLegacyKeyframeArray(out node.FrictionKeyframes, ref reader);
            ReadLegacyKeyframeArray(out node.ResistanceKeyframes, ref reader);
        }

        [BurstCompile]
        private static void ReadLegacyKeyframeArray<T>(out NativeArray<T> output, ref BinaryReader reader) where T : unmanaged {
            reader.ReadArray(out NativeArray<KeyframeV1> legacy, Allocator.Temp);
            output = new NativeArray<T>(legacy.Length, Allocator.Temp);

            for (int i = 0; i < legacy.Length; i++) {
                var currentKeyframe = legacy[i].ToCurrentKeyframe();
                unsafe {
                    output[i] = *(T*)(&currentKeyframe);
                }
            }

            legacy.Dispose();
        }
    }
}
