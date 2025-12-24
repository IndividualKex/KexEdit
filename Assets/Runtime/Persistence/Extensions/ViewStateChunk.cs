using Unity.Mathematics;

namespace KexEdit.Persistence {
    public struct ViewStateChunk {
        public float TimelineOffset;
        public float TimelineZoom;
        public float GraphPanX;
        public float GraphPanY;
        public float GraphZoom;
        public float3 CameraPosition;
        public float3 CameraTargetPosition;
        public float CameraDistance;
        public float CameraTargetDistance;
        public float CameraPitch;
        public float CameraTargetPitch;
        public float CameraYaw;
        public float CameraTargetYaw;
        public float CameraSpeedMultiplier;

        public static ViewStateChunk Default => new() {
            TimelineOffset = 0f,
            TimelineZoom = 1f,
            GraphPanX = 0f,
            GraphPanY = 0f,
            GraphZoom = 1f,
            CameraPosition = float3.zero,
            CameraTargetPosition = float3.zero,
            CameraDistance = 50f,
            CameraTargetDistance = 50f,
            CameraPitch = 30f,
            CameraTargetPitch = 30f,
            CameraYaw = 0f,
            CameraTargetYaw = 0f,
            CameraSpeedMultiplier = 1f
        };
    }
}
