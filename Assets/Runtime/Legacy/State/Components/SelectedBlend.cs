using Unity.Entities;

namespace KexEdit.Legacy {
    public struct SelectedBlend : IComponentData {
        public float Value;

        public static implicit operator float(SelectedBlend blend) => blend.Value;
        public static implicit operator SelectedBlend(float value) => new() { Value = value };
    }
}
