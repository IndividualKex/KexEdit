using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public struct WheelAssembly : IBufferElementData {
        public Entity Section;
        public float3 WorldPosition;
        public quaternion WorldRotation;
        public float Offset;
        public float TrackGauge;
        public float Position;
    }
}
