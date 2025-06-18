using Unity.Entities;

namespace KexEdit {
    public struct Dirty : IComponentData {
        public bool Value;

        public static implicit operator bool(Dirty dirty) => dirty.Value;
        public static implicit operator Dirty(bool value) => new() { Value = value };
    }
}
