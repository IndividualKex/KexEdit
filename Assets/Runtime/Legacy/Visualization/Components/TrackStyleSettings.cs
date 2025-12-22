using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TrackStyleSettings : IComponentData {
        public int DefaultStyle;
        public int Version;
        public bool AutoStyle;
    }
}
