using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TrackStyleReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(TrackStyleReference reference) => reference.Value;
        public static implicit operator TrackStyleReference(Entity entity) => new() { Value = entity };
    }
}
