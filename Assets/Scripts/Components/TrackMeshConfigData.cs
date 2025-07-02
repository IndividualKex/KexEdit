using System;
using System.Collections.Generic;
using UnityEngine;

namespace KexEdit {
    [Serializable]
    public class TrackMeshConfigData {
        public float Spacing = 0.4f;
        public List<DuplicationMeshConfigData> DuplicationMeshes = new();
        public List<ExtrusionMeshConfigData> ExtrusionMeshes = new();
        public List<DuplicationGizmoConfigData> DuplicationGizmos = new();
        public List<ExtrusionGizmoConfigData> ExtrusionGizmos = new();
    }

    [Serializable]
    public class DuplicationMeshConfigData {
        public string MeshPath;
        public int Step = 1;
        public Color Color = Color.white;
        public string TexturePath;
    }

    [Serializable]
    public class ExtrusionMeshConfigData {
        public string MeshPath;
        public Color Color = Color.white;
        public string TexturePath;
    }

    [Serializable]
    public class DuplicationGizmoConfigData {
        public float StartHeart;
        public float EndHeart;
        public Color Color = Color.white;
    }

    [Serializable]
    public class ExtrusionGizmoConfigData {
        public float Heart;
        public Color Color = Color.white;
    }
}
