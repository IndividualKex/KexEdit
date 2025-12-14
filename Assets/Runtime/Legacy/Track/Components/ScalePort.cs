using Unity.Entities;

namespace KexEdit {
    public struct ScalePort : IComponentData {
        public float Value;

        public static implicit operator float(ScalePort port) => port.Value;
        public static implicit operator ScalePort(float value) => new() { Value = value };
    }
}
