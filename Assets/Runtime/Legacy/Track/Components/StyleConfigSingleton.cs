using Unity.Entities;

namespace KexEdit.Legacy {
    public struct StyleConfigSingleton : IComponentData {
        public int DefaultStyleIndex;
        public int StyleCount;
        public int Version;
    }
}
