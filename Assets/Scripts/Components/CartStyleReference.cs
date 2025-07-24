using Unity.Entities;

namespace KexEdit {
    public struct CartStyleReference : IComponentData {
        public int StyleIndex;
        public int Version;
    }
}