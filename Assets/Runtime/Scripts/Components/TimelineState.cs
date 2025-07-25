using Unity.Entities;

namespace KexEdit {
    public struct TimelineState : IComponentData {
        public float Offset;
        public float Zoom;
    }
}
