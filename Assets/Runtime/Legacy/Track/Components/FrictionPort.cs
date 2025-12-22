using Unity.Entities;

namespace KexEdit.Legacy {
    public struct FrictionPort : IComponentData {
        public float Value;

        public static implicit operator float(FrictionPort port) => port.Value;
        public static implicit operator FrictionPort(float value) => new() { Value = value };
    }
}
