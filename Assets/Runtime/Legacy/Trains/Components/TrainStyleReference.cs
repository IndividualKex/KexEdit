using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TrainStyleReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(TrainStyleReference reference) => reference.Value;
        public static implicit operator TrainStyleReference(Entity entity) => new() { Value = entity };
    }
}
