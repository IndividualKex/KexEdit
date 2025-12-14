using Unity.Entities;

namespace KexEdit {
    public struct WheelAssemblyMeshReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(WheelAssemblyMeshReference reference) => reference.Value;
        public static implicit operator WheelAssemblyMeshReference(Entity entity) => new() { Value = entity };
    }
}
