using Unity.Entities;

namespace KexEdit.Legacy {
    public struct StartPort : IComponentData {
        public float Value;

        public static implicit operator float(StartPort port) => port.Value;
        public static implicit operator StartPort(float value) => new() { Value = value };
    }
}