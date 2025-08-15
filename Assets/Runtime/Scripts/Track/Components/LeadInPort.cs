using Unity.Entities;

namespace KexEdit {
    public struct LeadInPort : IComponentData {
        public float Value;

        public static implicit operator float(LeadInPort port) => port.Value;
        public static implicit operator LeadInPort(float value) => new() { Value = value };
    }
}
