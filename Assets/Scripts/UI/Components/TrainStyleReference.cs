using Unity.Entities;

namespace KexEdit.UI {
    public struct TrainStyleReference : IComponentData {
        public int StyleIndex;
        public int Version;
    }
}
