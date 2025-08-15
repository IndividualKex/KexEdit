using Unity.Entities;

namespace KexEdit {
    public struct AxisPort : IComponentData {
        public float Value;

        public static implicit operator float(AxisPort port) => port.Value;
        public static implicit operator AxisPort(float value) => new() { Value = value };
    }
}
