using Unity.Entities;

namespace KexEdit.Legacy {
    public struct RollPort : IComponentData {
        public float Value;

        public static implicit operator float(RollPort port) => port.Value;
        public static implicit operator RollPort(float value) => new() { Value = value };
    }
}
