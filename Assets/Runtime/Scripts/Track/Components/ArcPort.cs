using Unity.Entities;

namespace KexEdit {
    public struct ArcPort : IComponentData {
        public float Value;

        public static implicit operator float(ArcPort port) => port.Value;
        public static implicit operator ArcPort(float value) => new() { Value = value };
    }
}
