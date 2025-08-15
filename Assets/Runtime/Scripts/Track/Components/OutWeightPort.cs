using Unity.Entities;

namespace KexEdit {
    public struct OutWeightPort : IComponentData {
        public float Value;

        public static implicit operator float(OutWeightPort port) => port.Value;
        public static implicit operator OutWeightPort(float value) => new() { Value = value };
    }
}