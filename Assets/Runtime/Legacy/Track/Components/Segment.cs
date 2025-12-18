using Unity.Entities;

namespace KexEdit.Legacy {
    public struct Segment : IComponentData {
        public Entity Style;
        public float StartTime;
        public float EndTime;
        public int StyleSettingsVersion;
        public uint StyleHash;
    }
}
