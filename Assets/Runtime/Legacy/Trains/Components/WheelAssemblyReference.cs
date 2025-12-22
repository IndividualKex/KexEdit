using Unity.Entities;

namespace KexEdit.Legacy {
    public struct WheelAssemblyReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(WheelAssemblyReference reference) => reference.Value;
        public static implicit operator WheelAssemblyReference(Entity entity) => new() { Value = entity };
    }
}
