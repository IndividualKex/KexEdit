using System;
using System.Collections.Generic;
using UnityEngine;

namespace KexEdit {
    [Serializable]
    public class TrackStyleSettingsData {
        public List<TrackStyleData> Styles = new();
        public int DefaultStyle;
        public int Version;
        public bool AutoStyle;
    }

    [Serializable]
    public class TrackStyleData {
        public List<DuplicationMeshSettingsData> DuplicationMeshes = new();
        public List<ExtrusionMeshSettingsData> ExtrusionMeshes = new();
        public List<CapMeshSettingsData> StartCapMeshes = new();
        public List<CapMeshSettingsData> EndCapMeshes = new();
        public float Spacing;
        public float Threshold;
    }

    [Serializable]
    public class DuplicationMeshSettingsData {
        public Mesh Mesh;
        public Material Material;
        public int Step = 1;
        public int Offset = 0;
    }

    [Serializable]
    public class ExtrusionMeshSettingsData {
        public Mesh Mesh;
        public Material Material;
    }

    [Serializable]
    public class CapMeshSettingsData {
        public Mesh Mesh;
        public Material Material;
    }
}
