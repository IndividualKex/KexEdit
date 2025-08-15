using Unity.Entities;

namespace KexEdit {
    public struct HeartPort : IComponentData {
        public float Value;

        public static implicit operator float(HeartPort port) => port.Value;
        public static implicit operator HeartPort(float value) => new() { Value = value };
    }
}
