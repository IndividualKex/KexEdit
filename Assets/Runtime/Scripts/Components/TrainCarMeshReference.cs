using Unity.Entities;

namespace KexEdit {
    public struct TrainCarMeshReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(TrainCarMeshReference reference) => reference.Value;
        public static implicit operator TrainCarMeshReference(Entity entity) => new() { Value = entity };
    }
}
