using Unity.Entities;

namespace KexEdit.Legacy {
    public struct InputPortReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(InputPortReference port) => port.Value;
        public static implicit operator InputPortReference(Entity value) => new() { Value = value };
    }
}
