using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    public struct CameraState : IComponentData {
        public float3 Position;
        public float3 TargetPosition;
        public float Distance;
        public float TargetDistance;
        public float Pitch;
        public float TargetPitch;
        public float Yaw;
        public float TargetYaw;
        public float SpeedMultiplier;
        public float OrthographicSize;
        public float TargetOrthographicSize;
        public bool Orthographic;
        public bool TargetOrthographic;
    }
}
