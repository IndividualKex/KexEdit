using System;
using System.Collections.Generic;
using UnityEngine;

namespace KexEdit {
    [Serializable]
    public class TrackMeshConfig {
        public string Name;
        public List<DuplicationMeshConfig> DuplicationMeshes = new();
        public List<ExtrusionMeshConfig> ExtrusionMeshes = new();
        public float Spacing = 0.4f;
    }

    [Serializable]
    public class DuplicationMeshConfig {
        public string MeshPath;
        public int Step = 1;
        public Color Color = Color.white;
        public string TexturePath;
    }

    [Serializable]
    public class ExtrusionMeshConfig {
        public string MeshPath;
        public Color Color = Color.white;
        public string TexturePath;
    }

}
