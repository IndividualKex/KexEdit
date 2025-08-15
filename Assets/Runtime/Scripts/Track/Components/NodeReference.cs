using Unity.Entities;

namespace KexEdit {
    public struct NodeReference : IComponentData {
        public Entity Value;

        public static implicit operator Entity(NodeReference reference) => reference.Value;
        public static implicit operator NodeReference(Entity value) => new() { Value = value };
    }
}
