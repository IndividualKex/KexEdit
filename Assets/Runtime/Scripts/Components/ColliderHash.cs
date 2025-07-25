using Unity.Entities;

namespace KexEdit {
    public struct ColliderHash : IComponentData {
        public uint Value;

        public static implicit operator uint(ColliderHash hash) => hash.Value;
        public static implicit operator ColliderHash(uint value) => new() { Value = value };
    }
}
