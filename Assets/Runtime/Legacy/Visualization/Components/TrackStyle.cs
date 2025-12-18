using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TrackStyle : IComponentData {
        public Entity Settings;
        public float Spacing;
        public float Threshold;
        public int Step;
    }
}
