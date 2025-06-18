using Unity.Entities;

namespace KexEdit {
    public struct TrackHash : IComponentData {
        public uint Value;

        public static implicit operator uint(TrackHash hash) => hash.Value;
        public static implicit operator TrackHash(uint value) => new() { Value = value };
    }
}
