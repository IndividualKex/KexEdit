using Unity.Entities;

namespace KexEdit {
    public struct TrackColliderReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(TrackColliderReference reference) => reference.Value;
        public static implicit operator TrackColliderReference(Entity value) => new() { Value = value };
    }
}
