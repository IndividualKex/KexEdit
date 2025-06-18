using Unity.Entities;

namespace KexEdit {
    public struct LeadOutPort : IComponentData {
        public float Value;

        public static implicit operator float(LeadOutPort port) => port.Value;
        public static implicit operator LeadOutPort(float value) => new() { Value = value };
    }
}
