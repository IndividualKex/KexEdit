using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.UI.Serialization {
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
        public const int CURRENT = UI_STATE_SERIALIZATION;
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
        Reserved1 = 1 << 7,
        Reserved2 = 1 << 8,
        Reserved3 = 1 << 9,
        Reserved4 = 1 << 10,
        Reserved5 = 1 << 11,
        Reserved6 = 1 << 12,
        Reserved7 = 1 << 13,
        Reserved8 = 1 << 14,
        Reserved9 = 1 << 15,
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

        public static SerializedUIState FromState(UIState state) {
            return new SerializedUIState {
                TimelineOffset = state.TimelineOffset,
                TimelineZoom = state.TimelineZoom,
                NodeGraphPanX = state.NodeGraphPan.x,
                NodeGraphPanY = state.NodeGraphPan.y,
                NodeGraphZoom = state.NodeGraphZoom,
                CameraPositionX = state.CameraPosition.x,
                CameraPositionY = state.CameraPosition.y,
                CameraPositionZ = state.CameraPosition.z,
                CameraTargetPositionX = state.CameraTargetPosition.x,
                CameraTargetPositionY = state.CameraTargetPosition.y,
                CameraTargetPositionZ = state.CameraTargetPosition.z,
                CameraDistance = state.CameraDistance,
                CameraTargetDistance = state.CameraTargetDistance,
                CameraPitch = state.CameraPitch,
                CameraTargetPitch = state.CameraTargetPitch,
                CameraYaw = state.CameraYaw,
                CameraTargetYaw = state.CameraTargetYaw,
                CameraSpeedMultiplier = state.CameraSpeedMultiplier
            };
        }

        public UIState ToState() {
            return new UIState {
                TimelineOffset = TimelineOffset,
                TimelineZoom = TimelineZoom,
                NodeGraphPan = new float2(NodeGraphPanX, NodeGraphPanY),
                NodeGraphZoom = NodeGraphZoom,
                CameraPosition = new float3(CameraPositionX, CameraPositionY, CameraPositionZ),
                CameraTargetPosition = new float3(CameraTargetPositionX, CameraTargetPositionY, CameraTargetPositionZ),
                CameraDistance = CameraDistance,
                CameraTargetDistance = CameraTargetDistance,
                CameraPitch = CameraPitch,
                CameraTargetPitch = CameraTargetPitch,
                CameraYaw = CameraYaw,
                CameraTargetYaw = CameraTargetYaw,
                CameraSpeedMultiplier = CameraSpeedMultiplier
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

        public bool Render {
            get => (BooleanFlags & NodeFlags.Render) != 0;
            set => BooleanFlags = value ? BooleanFlags | NodeFlags.Render : BooleanFlags & ~NodeFlags.Render;
        }

        public bool Selected {
            get => (BooleanFlags & NodeFlags.Selected) != 0;
            set => BooleanFlags = value ? BooleanFlags | NodeFlags.Selected : BooleanFlags & ~NodeFlags.Selected;
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
