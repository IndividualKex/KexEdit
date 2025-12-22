using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy.Serialization {
    [Serializable]
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

    [Serializable]
    internal struct NodeV1 {
#pragma warning disable 0649
        public float2 Position;
        public NodeType Type;
        public int Priority;
        [MarshalAs(UnmanagedType.U1)]
        public bool Selected;

        public Entity Next;
        public Entity Previous;
#pragma warning restore 0649

        public Node ToCurrentNode(uint id) {
            return new Node {
                Id = id,
                Position = Position,
                Type = Type,
                Priority = Priority,
                Selected = Selected,
                Next = Next,
                Previous = Previous
            };
        }
    }

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
        public NativeArray<Keyframe> RollSpeedKeyframes;
        public NativeArray<Keyframe> NormalForceKeyframes;
        public NativeArray<Keyframe> LateralForceKeyframes;
        public NativeArray<Keyframe> PitchSpeedKeyframes;
        public NativeArray<Keyframe> YawSpeedKeyframes;
        public NativeArray<Keyframe> FixedVelocityKeyframes;
        public NativeArray<Keyframe> HeartKeyframes;
        public NativeArray<Keyframe> FrictionKeyframes;
        public NativeArray<Keyframe> ResistanceKeyframes;
        public NativeArray<Keyframe> TrackStyleKeyframes;

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
