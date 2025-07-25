using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class TrackStyleSettings : IComponentData {
        public List<TrackStyle> Styles = new();
        public int DefaultStyle;
        public int Version;
        public bool AutoStyle;
    }
}
