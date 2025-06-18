using Unity.Entities;

namespace KexEdit {
    public struct Render : IComponentData {
        public bool Value;

        public static implicit operator bool(Render render) => render.Value;
        public static implicit operator Render(bool value) => new() { Value = value };
    }
}
