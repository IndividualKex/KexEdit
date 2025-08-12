using Unity.Entities;

namespace KexEdit {
    public struct ExtrusionMeshReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(ExtrusionMeshReference reference) => reference.Value;
        public static implicit operator ExtrusionMeshReference(Entity entity) => new() { Value = entity };
    }
}
