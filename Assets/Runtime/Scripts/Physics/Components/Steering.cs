using Unity.Entities;

namespace KexEdit {
    public struct Steering : IComponentData {
        public bool Value;

        public static implicit operator bool(Steering steering) => steering.Value;
        public static implicit operator Steering(bool value) => new() { Value = value };
    }
}