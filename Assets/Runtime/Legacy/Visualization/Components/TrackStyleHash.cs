using Unity.Entities;

namespace KexEdit {
    public struct TrackStyleHash : IComponentData {
        public uint Value;

        public static implicit operator uint(TrackStyleHash hash) => hash.Value;
        public static implicit operator TrackStyleHash(uint value) => new() { Value = value };
    }
}
