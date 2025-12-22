using Unity.Entities;

namespace KexEdit.Legacy {
    public struct OutputPortReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(OutputPortReference port) => port.Value;
        public static implicit operator OutputPortReference(Entity value) => new() { Value = value };
    }
}
