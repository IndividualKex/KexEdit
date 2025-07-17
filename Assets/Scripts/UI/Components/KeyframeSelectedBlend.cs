using Unity.Entities;
using Unity.Rendering;

namespace KexEdit.UI {
    [MaterialProperty("_SelectedBlend")]
    public struct KeyframeSelectedBlend : IComponentData {
        public float Value;

        public static implicit operator float(KeyframeSelectedBlend blend) => blend.Value;
        public static implicit operator KeyframeSelectedBlend(float value) => new() { Value = value };
    }
}
