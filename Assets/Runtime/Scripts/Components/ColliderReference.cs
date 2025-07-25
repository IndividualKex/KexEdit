using Unity.Entities;

namespace KexEdit {
    public struct ColliderReference : IBufferElementData {
        public Entity Value;

        public static implicit operator Entity(ColliderReference reference) => reference.Value;
        public static implicit operator ColliderReference(Entity value) => new() { Value = value };
    }
}
