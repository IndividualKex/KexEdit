using Unity.Entities;

namespace KexEdit {
    public struct Segment : IComponentData {
        public Entity Style;
        public float StartTime;
        public float EndTime;
        public int StyleVersion;
        public uint StyleHash;
        public bool HasBuffers;
    }
}
