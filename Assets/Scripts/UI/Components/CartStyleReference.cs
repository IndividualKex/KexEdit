using Unity.Entities;

namespace KexEdit.UI {
    public struct CartStyleReference : IComponentData {
        public int StyleIndex;
        public int Version;
    }
}
