using Unity.Entities;

namespace KexEdit {
    public struct RadiusPort : IComponentData {
        public float Value;

        public static implicit operator float(RadiusPort port) => port.Value;
        public static implicit operator RadiusPort(float value) => new() { Value = value };
    }
}
