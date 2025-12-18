using System;
using System.Collections.Generic;

namespace KexEdit.Legacy {
    [Serializable]
    public class TrainStyleData {
        public List<TrainCarData> TrainCars = new();
        public int Version;
    }

    [Serializable]
    public class TrainCarData {
        public string MeshPath;
        public float Offset;
        public List<WheelAssemblyData> WheelAssemblies = new();
    }

    [Serializable]
    public class WheelAssemblyData {
        public string MeshPath;
        public float Offset;
    }
}
