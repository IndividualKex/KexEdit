using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class TrackStyleSettings : IComponentData {
        public List<TrackStyle> Styles = new();
        public int Version;
        public int DefaultStyle;
    }
}
