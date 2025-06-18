using Unity.Entities;

namespace KexEdit {
    public struct ColliderPrefabReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(ColliderPrefabReference prefab) => prefab.Value;
        public static implicit operator ColliderPrefabReference(Entity value) => new() { Value = value };
    }
}
