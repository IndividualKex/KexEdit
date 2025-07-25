using System;
using System.Collections.Generic;

namespace KexEdit.UI {
    [Serializable]
    public class CartStyleConfig {
        public string Name;
        public List<CartStyleMeshConfig> Styles = new();

        [NonSerialized]
        public string SourceFileName;
    }

    [Serializable]
    public class CartStyleMeshConfig {
        public string MeshPath;
    }
}
