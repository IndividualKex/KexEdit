using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TrackFollower : IComponentData {
        public Entity Section;
        public float Index;
        public float ProjectionDistance;
        public bool Active;
        public bool OutOfBounds;

        public static TrackFollower Default => new() {
            Section = Entity.Null,
            Index = 1f,
            ProjectionDistance = 0f,
            Active = false,
            OutOfBounds = false,
        };
    }
}
