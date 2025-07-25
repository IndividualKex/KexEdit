using UnityEngine;
using System;

namespace KexEdit {
    [Serializable]
    public class DuplicationMeshSettings {
        public Mesh Mesh;
        public Material Material;
        public int Step = 1;
        public int Offset = 0;
    }
}
