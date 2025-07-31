using Unity.Entities;

namespace KexEdit {
    public struct TimelineState : IComponentData {
        public float Offset;
        public float Zoom;

        public static readonly TimelineState Default = new() {
            Offset = 0f,
            Zoom = 1f
        };
    }
}
