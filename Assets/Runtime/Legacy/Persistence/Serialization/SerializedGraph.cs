using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Legacy.Serialization {
    /*
     * HOW TO ADD NEW FIELDS TO SERIALIZATION:
     * 1. Add field to SerializedNode struct
     * 2. Add corresponding flag to NodeFieldFlags enum  
     * 3. Update GraphSerializer.SerializeNode() to write field conditionally
     * 4. Update GraphSerializer.DeserializeNode() to read field conditionally
     * 5. Update SizeCalculator.CalculateNodeSize() to include field size
     * 6. Set flag in SerializationSystem.SerializeNode() when field has data
     */

    public static class SerializationVersion {
        public const int INITIAL = 1;
        public const int PRECISION_MIGRATION = 2;
        public const int UI_STATE_SERIALIZATION = 3;
        public const int TRACK_STYLE_PROPERTY = 4;
        public const int COPY_PATH_TRIM_PORTS = 5;
        public const int NODE_ID = 6;
        public const int BRIDGE_WEIGHT_PORTS = 7;
        public const int CURRENT = BRIDGE_WEIGHT_PORTS;
    }

    [Flags]
    public enum NodeFieldFlags : uint {
        None = 0,
        HasRender = 1 << 0,
        HasSelected = 1 << 1,
        HasPropertyOverrides = 1 << 2,
        HasSelectedProperties = 1 << 3,
        HasCurveData = 1 << 4,
        HasDuration = 1 << 5,
        HasMeshFilePath = 1 << 6,
        HasSteering = 1 << 7,
        Reserved1 = 1 << 8,
        Reserved2 = 1 << 9,
        Reserved3 = 1 << 10,
        Reserved4 = 1 << 11,
        Reserved5 = 1 << 12,
        Reserved6 = 1 << 13,
        Reserved7 = 1 << 14,
        Reserved8 = 1 << 15,
    }

    public struct SerializedUIState {
        public float TimelineOffset;
        public float TimelineZoom;
        public float NodeGraphPanX;
        public float NodeGraphPanY;
        public float NodeGraphZoom;
        public float CameraPositionX;
        public float CameraPositionY;
        public float CameraPositionZ;
        public float CameraTargetPositionX;
        public float CameraTargetPositionY;
        public float CameraTargetPositionZ;
        public float CameraDistance;
        public float CameraTargetDistance;
        public float CameraPitch;
        public float CameraTargetPitch;
        public float CameraYaw;
        public float CameraTargetYaw;
        public float CameraSpeedMultiplier;

        public static SerializedUIState FromState(
            TimelineState timelineState,
            NodeGraphState nodeGraphState,
            CameraState cameraState
        ) {
            return new SerializedUIState {
                TimelineOffset = timelineState.Offset,
                TimelineZoom = timelineState.Zoom,
                NodeGraphPanX = nodeGraphState.Pan.x,
                NodeGraphPanY = nodeGraphState.Pan.y,
                NodeGraphZoom = nodeGraphState.Zoom,
                CameraPositionX = cameraState.Position.x,
                CameraPositionY = cameraState.Position.y,
                CameraPositionZ = cameraState.Position.z,
                CameraTargetPositionX = cameraState.TargetPosition.x,
                CameraTargetPositionY = cameraState.TargetPosition.y,
                CameraTargetPositionZ = cameraState.TargetPosition.z,
                CameraDistance = cameraState.Distance,
                CameraTargetDistance = cameraState.TargetDistance,
                CameraPitch = cameraState.Pitch,
                CameraTargetPitch = cameraState.TargetPitch,
                CameraYaw = cameraState.Yaw,
                CameraTargetYaw = cameraState.TargetYaw,
                CameraSpeedMultiplier = cameraState.SpeedMultiplier
            };
        }

        public void ToState(out TimelineState timelineState, out NodeGraphState nodeGraphState, out CameraState cameraState) {
            timelineState = new TimelineState {
                Offset = TimelineOffset,
                Zoom = TimelineZoom,
            };
            nodeGraphState = new NodeGraphState {
                Pan = new float2(NodeGraphPanX, NodeGraphPanY),
                Zoom = NodeGraphZoom,
            };
            cameraState = new CameraState {
                Position = new float3(CameraPositionX, CameraPositionY, CameraPositionZ),
                TargetPosition = new float3(CameraTargetPositionX, CameraTargetPositionY, CameraTargetPositionZ),
                Distance = CameraDistance,
                TargetDistance = CameraTargetDistance,
                Pitch = CameraPitch,
                TargetPitch = CameraTargetPitch,
                Yaw = CameraYaw,
                TargetYaw = CameraTargetYaw,
                SpeedMultiplier = CameraSpeedMultiplier
            };
        }
    }

    public struct SerializedGraph : IDisposable {
        public int Version;
        public SerializedUIState UIState;
        public NativeArray<SerializedNode> Nodes;
        public NativeArray<SerializedEdge> Edges;

        public void Dispose() {
            if (Nodes.IsCreated) Nodes.Dispose();
            if (Edges.IsCreated) Edges.Dispose();
        }
    }

    public struct SerializedNode : IDisposable {
        public Node Node;
        public PointData Anchor;
        public NodeFieldFlags FieldFlags;
        public NodeFlags BooleanFlags;
        public PropertyOverrides PropertyOverrides;
        public SelectedProperties SelectedProperties;
        public CurveData CurveData;
        public Duration Duration;
        public FixedString512Bytes MeshFilePath;

        public NativeArray<SerializedPort> InputPorts;
        public NativeArray<SerializedPort> OutputPorts;
        public NativeArray<RollSpeedKeyframe> RollSpeedKeyframes;
        public NativeArray<NormalForceKeyframe> NormalForceKeyframes;
        public NativeArray<LateralForceKeyframe> LateralForceKeyframes;
        public NativeArray<PitchSpeedKeyframe> PitchSpeedKeyframes;
        public NativeArray<YawSpeedKeyframe> YawSpeedKeyframes;
        public NativeArray<FixedVelocityKeyframe> FixedVelocityKeyframes;
        public NativeArray<HeartKeyframe> HeartKeyframes;
        public NativeArray<FrictionKeyframe> FrictionKeyframes;
        public NativeArray<ResistanceKeyframe> ResistanceKeyframes;
        public NativeArray<TrackStyleKeyframe> TrackStyleKeyframes;

        public bool Render {
            get => (BooleanFlags & NodeFlags.Render) != 0;
            set => BooleanFlags = value ? BooleanFlags | NodeFlags.Render : BooleanFlags & ~NodeFlags.Render;
        }

        public bool Selected {
            get => (BooleanFlags & NodeFlags.Selected) != 0;
            set => BooleanFlags = value ? BooleanFlags | NodeFlags.Selected : BooleanFlags & ~NodeFlags.Selected;
        }

        public bool Steering {
            get => (BooleanFlags & NodeFlags.Steering) != 0;
            set => BooleanFlags = value ? BooleanFlags | NodeFlags.Steering : BooleanFlags & ~NodeFlags.Steering;
        }

        public void Dispose() {
            if (InputPorts.IsCreated) InputPorts.Dispose();
            if (OutputPorts.IsCreated) OutputPorts.Dispose();
            if (RollSpeedKeyframes.IsCreated) RollSpeedKeyframes.Dispose();
            if (NormalForceKeyframes.IsCreated) NormalForceKeyframes.Dispose();
            if (LateralForceKeyframes.IsCreated) LateralForceKeyframes.Dispose();
            if (PitchSpeedKeyframes.IsCreated) PitchSpeedKeyframes.Dispose();
            if (YawSpeedKeyframes.IsCreated) YawSpeedKeyframes.Dispose();
            if (FixedVelocityKeyframes.IsCreated) FixedVelocityKeyframes.Dispose();
            if (HeartKeyframes.IsCreated) HeartKeyframes.Dispose();
            if (FrictionKeyframes.IsCreated) FrictionKeyframes.Dispose();
            if (ResistanceKeyframes.IsCreated) ResistanceKeyframes.Dispose();
            if (TrackStyleKeyframes.IsCreated) TrackStyleKeyframes.Dispose();
        }
    }

    public struct SerializedPort {
        public Port Port;
        public PointData Value;
    }

    public struct SerializedEdge {
        public uint Id;
        public uint SourceId;
        public uint TargetId;
        [MarshalAs(UnmanagedType.U1)]
        public bool Selected;
    }
}
