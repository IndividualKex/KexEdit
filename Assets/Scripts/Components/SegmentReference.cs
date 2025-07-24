using Unity.Entities;

namespace KexEdit {
    public struct SegmentReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(SegmentReference reference) => reference.Value;
        public static implicit operator SegmentReference(Entity entity) => new() { Value = entity };
    }
}
