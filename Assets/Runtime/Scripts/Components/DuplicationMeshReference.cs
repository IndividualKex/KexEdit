using Unity.Entities;

namespace KexEdit {
    public struct DuplicationMeshReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(DuplicationMeshReference reference) => reference.Value;
        public static implicit operator DuplicationMeshReference(Entity entity) => new() { Value = entity };
    }
}
