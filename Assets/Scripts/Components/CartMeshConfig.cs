using System;
using System.Collections.Generic;
using UnityEngine;

namespace KexEdit {
    [Serializable]
    public class CartMeshConfig {
        public string Name;
        public List<CartStyleConfig> Styles = new();

        [NonSerialized]
        public string SourceFileName;
    }

    [Serializable]
    public class CartStyleConfig {
        public string MeshPath;
    }
}