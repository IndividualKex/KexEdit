using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public struct TrackPoint : IBufferElementData {
        public float3 Position;
        public float3 Direction;
        public float3 Normal;
        public float Velocity;
        public float Distance;
        public float Time;
    }
}
