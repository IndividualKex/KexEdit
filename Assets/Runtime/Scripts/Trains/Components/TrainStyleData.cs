using System;
using System.Collections.Generic;

namespace KexEdit {
    [Serializable]
    public class TrainStyleData {
        public List<TrainCarData> TrainCars = new();
        public int Version;
        
        public int CarCount;
        public float CarSpacing = 3.0f;
        public TrainCarTemplateData DefaultCar;
        public List<TrainCarOverrideData> CarOverrides;
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

    [Serializable]
    public class TrainCarTemplateData {
        public string MeshPath;
        public List<WheelAssemblyTemplateData> WheelAssemblies;
    }

    [Serializable]
    public class TrainCarOverrideData {
        public int Index;
        public string MeshPath;
        public List<WheelAssemblyTemplateData> WheelAssemblies;
    }

    [Serializable]
    public class WheelAssemblyTemplateData {
        public string MeshPath;
        public float Offset;
    }
}
