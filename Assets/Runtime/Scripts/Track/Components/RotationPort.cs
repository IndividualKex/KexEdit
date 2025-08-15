using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public struct RotationPort : IComponentData {
        public float3 Value;

        public static implicit operator float3(RotationPort port) => port.Value;
        public static implicit operator RotationPort(float3 value) => new() { Value = value };
    }
}
