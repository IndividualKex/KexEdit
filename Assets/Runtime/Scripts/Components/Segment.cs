using Unity.Entities;

namespace KexEdit {
    public struct Segment : IComponentData {
        public float StartTime;
        public float EndTime;
        public uint StyleHash;
    }
}
