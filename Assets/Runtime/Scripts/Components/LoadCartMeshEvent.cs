using Unity.Entities;

namespace KexEdit {
    public struct LoadCartMeshEvent : IComponentData {
        public Entity Target;
        public Entity Cart;
    }
}
