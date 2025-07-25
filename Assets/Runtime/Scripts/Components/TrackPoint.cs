using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public struct TrackPoint : IBufferElementData {
        public float3 Position;
        public float3 Direction;
        public float3 Normal;
        public float Distance;
        public float Heart;
        public float Time;
        public float VisualizationValue;
    }
}
