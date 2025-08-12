using Unity.Entities;

namespace KexEdit {
    public struct EndCapMeshReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(EndCapMeshReference reference) => reference.Value;
        public static implicit operator EndCapMeshReference(Entity entity) => new() { Value = entity };
    }
}
