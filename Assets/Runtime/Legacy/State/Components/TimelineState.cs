using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TimelineState : IComponentData {
        public float Offset;
        public float Zoom;
        public float PlayheadTime;

        public static readonly TimelineState Default = new() {
            Offset = 0f,
            Zoom = 1f,
            PlayheadTime = 0f
        };
    }
}
