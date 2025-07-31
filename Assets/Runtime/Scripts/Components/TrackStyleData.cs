using System;
using System.Collections.Generic;

namespace KexEdit {
    [Serializable]
    public class TrackStyleData {
        public List<TrackStyleMeshData> Styles = new();
        public int DefaultStyle;
        public int Version;
        public bool AutoStyle;
    }

    [Serializable]
    public class TrackStyleMeshData {
        public List<DuplicationMeshSettings> DuplicationMeshes = new();
        public List<ExtrusionMeshSettings> ExtrusionMeshes = new();
        public List<CapMeshSettings> StartCapMeshes = new();
        public List<CapMeshSettings> EndCapMeshes = new();
        public float Spacing;
        public float Threshold;
    }
}
