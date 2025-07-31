using Unity.Entities;

namespace KexEdit {
    public struct CartReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(CartReference reference) => reference.Value;
        public static implicit operator CartReference(Entity entity) => new() { Value = entity };
    }
}
