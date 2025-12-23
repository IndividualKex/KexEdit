using System;
using System.Collections.Generic;

namespace KexEdit.UI {
    [Serializable]
    public class TrainStyleConfig {
        public string Name;

        public int CarCount;
        public float CarSpacing = 3.0f;

        public TrainCarTemplate DefaultCar;
        public List<TrainCarOverride> CarOverrides;

        [NonSerialized]
        public string SourceFileName;
    }

    [Serializable]
    public class TrainCarTemplate {
        public string MeshPath;
        public List<WheelAssemblyConfig> WheelAssemblies;
    }

    [Serializable]
    public class TrainCarOverride {
        public int Index;
        public string MeshPath;
        public List<WheelAssemblyConfig> WheelAssemblies;
    }

    [Serializable]
    public class WheelAssemblyConfig {
        public string MeshPath;
        public float Offset;
    }
}
