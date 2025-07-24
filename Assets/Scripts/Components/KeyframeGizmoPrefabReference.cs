using Unity.Entities;

namespace KexEdit {
    public struct KeyframeGizmoPrefabReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(KeyframeGizmoPrefabReference gizmo) => gizmo.Value;
        public static implicit operator KeyframeGizmoPrefabReference(Entity value) => new() { Value = value };
    }
}
