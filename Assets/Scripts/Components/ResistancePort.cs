using Unity.Entities;

namespace KexEdit {
    public struct ResistancePort : IComponentData {
        public float Value;

        public static implicit operator float(ResistancePort port) => port.Value;
        public static implicit operator ResistancePort(float value) => new() { Value = value };
    }
}
