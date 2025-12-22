using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Core.Articulation {
    [BurstCompile]
    public readonly struct BodyTransform {
        public readonly float3 Position;
        public readonly quaternion Rotation;

        public BodyTransform(float3 position, quaternion rotation) {
            Position = position;
            Rotation = rotation;
        }

        public static BodyTransform Identity => new(float3.zero, quaternion.identity);
    }
}
