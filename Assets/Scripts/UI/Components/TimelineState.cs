using Unity.Entities;

namespace KexEdit.UI {
    public struct TimelineState : IComponentData {
        public float Offset;
        public float Zoom;
    }
}
