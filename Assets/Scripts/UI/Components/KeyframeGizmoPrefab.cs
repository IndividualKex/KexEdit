using Unity.Entities;

namespace KexEdit.UI {
    public struct KeyframeGizmoPrefab : IComponentData {
        public Entity Value;

        public static implicit operator Entity(KeyframeGizmoPrefab gizmo) => gizmo.Value;
        public static implicit operator KeyframeGizmoPrefab(Entity value) => new() { Value = value };
    }
}
