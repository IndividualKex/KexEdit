using Unity.Entities;

namespace KexEdit {
    public struct LoadTrainMeshEvent : IComponentData {
        public Entity Target;
        public Entity Train;
    }
}
