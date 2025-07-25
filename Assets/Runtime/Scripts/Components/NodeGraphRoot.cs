using Unity.Entities;

namespace KexEdit {
    public struct NodeGraphRoot : IComponentData {
        public Entity Value;

        public static implicit operator Entity(NodeGraphRoot root) => root.Value;
        public static implicit operator NodeGraphRoot(Entity entity) => new() { Value = entity };
    }
}
