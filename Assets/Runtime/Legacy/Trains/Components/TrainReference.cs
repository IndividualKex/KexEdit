using Unity.Entities;

namespace KexEdit {
    public struct TrainReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(TrainReference reference) => reference.Value;
        public static implicit operator TrainReference(Entity entity) => new() { Value = entity };
    }
}
