using Unity.Entities;

namespace KexEdit {
    public struct InWeightPort : IComponentData {
        public float Value;

        public static implicit operator float(InWeightPort port) => port.Value;
        public static implicit operator InWeightPort(float value) => new() { Value = value };
    }
}