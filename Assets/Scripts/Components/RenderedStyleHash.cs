using Unity.Entities;

namespace KexEdit {
    public struct RenderedStyleHash : IComponentData {
        public uint Value;

        public static implicit operator uint(RenderedStyleHash version) => version.Value;
        public static implicit operator RenderedStyleHash(uint value) => new() { Value = value };
    }
}
