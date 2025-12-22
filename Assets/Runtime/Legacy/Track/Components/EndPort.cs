using Unity.Entities;

namespace KexEdit.Legacy {
    public struct EndPort : IComponentData {
        public float Value;

        public static implicit operator float(EndPort port) => port.Value;
        public static implicit operator EndPort(float value) => new() { Value = value };
    }
}