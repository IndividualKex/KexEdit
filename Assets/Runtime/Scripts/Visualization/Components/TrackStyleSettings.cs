using Unity.Entities;

namespace KexEdit {
    public struct TrackStyleSettings : IComponentData {
        public int DefaultStyle;
        public int Version;
        public bool AutoStyle;
    }
}
