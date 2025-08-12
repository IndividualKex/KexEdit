using Unity.Entities;

namespace KexEdit {
    public struct StartCapMeshReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(StartCapMeshReference reference) => reference.Value;
        public static implicit operator StartCapMeshReference(Entity entity) => new() { Value = entity };
    }
}
