using Unity.Entities;

namespace KexEdit {
    public struct StyleHash : IComponentData {
        public uint Value;

        public static implicit operator uint(StyleHash hash) => hash.Value;
        public static implicit operator StyleHash(uint value) => new() { Value = value };
    }
}
