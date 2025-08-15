using System;
using System.Collections.Generic;

namespace KexEdit.UI {
    [Serializable]
    public class TrainStyleConfig {
        public string Name;
        public List<TrainStyleMeshConfig> Styles = new();

        [NonSerialized]
        public string SourceFileName;
    }

    [Serializable]
    public class TrainStyleMeshConfig {
        public string MeshPath;
    }
}
