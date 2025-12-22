using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    public struct PositionPort : IComponentData {
        public float3 Value;

        public static implicit operator float3(PositionPort port) => port.Value;
        public static implicit operator PositionPort(float3 value) => new() { Value = value };
    }
}
