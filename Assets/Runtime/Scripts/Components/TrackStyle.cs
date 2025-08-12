using Unity.Entities;

namespace KexEdit {
    public struct TrackStyle : IComponentData {
        public Entity Settings;
        public float Spacing;
        public float Threshold;
    }
}
