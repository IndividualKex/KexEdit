using Unity.Entities;

namespace KexEdit {
    public struct TrackStyleReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(TrackStyleReference reference) => reference.Value;
        public static implicit operator TrackStyleReference(Entity entity) => new() { Value = entity };
    }
}
