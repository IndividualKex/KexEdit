using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public struct UIState : IComponentData {
        public float TimelineOffset;
        public float TimelineZoom;
        public float2 NodeGraphPan;
        public float NodeGraphZoom;
        public float3 CameraPosition;
        public float3 CameraTargetPosition;
        public float CameraDistance;
        public float CameraTargetDistance;
        public float CameraPitch;
        public float CameraTargetPitch;
        public float CameraYaw;
        public float CameraTargetYaw;
        public float CameraSpeedMultiplier;
        public float CameraOrthographicSize;
        public float CameraTargetOrthographicSize;
        public bool CameraOrthographic;
        public bool CameraTargetOrthographic;
    }
}
