using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TrackColliderHash : IComponentData {
        public uint Value;

        public static implicit operator uint(TrackColliderHash hash) => hash.Value;
        public static implicit operator TrackColliderHash(uint value) => new() { Value = value };
    }
}
