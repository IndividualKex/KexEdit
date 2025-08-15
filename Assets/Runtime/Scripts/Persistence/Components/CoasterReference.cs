using Unity.Entities;

namespace KexEdit {
    public struct CoasterReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(CoasterReference reference) => reference.Value;
        public static implicit operator CoasterReference(Entity entity) => new() { Value = entity };
    }
}
