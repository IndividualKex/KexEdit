using Unity.Entities;

namespace KexEdit.Legacy {
    public struct YawPort : IComponentData {
        public float Value;

        public static implicit operator float(YawPort port) => port.Value;
        public static implicit operator YawPort(float value) => new() { Value = value };
    }
}
