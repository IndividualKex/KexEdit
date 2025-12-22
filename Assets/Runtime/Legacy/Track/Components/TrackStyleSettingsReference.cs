using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TrackStyleSettingsReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(TrackStyleSettingsReference reference) => reference.Value;
        public static implicit operator TrackStyleSettingsReference(Entity entity) => new() { Value = entity };
    }
}
