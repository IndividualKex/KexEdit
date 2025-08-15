using Unity.Entities;

namespace KexEdit {
    public struct TrainCarReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(TrainCarReference reference) => reference.Value;
        public static implicit operator TrainCarReference(Entity entity) => new() { Value = entity };
    }
}
