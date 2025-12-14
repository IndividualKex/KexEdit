using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Core.Articulation {
    [BurstCompile]
    public readonly struct Anchor {
        public readonly float3 Position;
        public readonly float3 Direction;
        public readonly float3 Normal;
        public readonly float3 Lateral;
        public readonly float Arc;

        public Anchor(float3 position, float3 direction, float3 normal, float3 lateral, float arc) {
            Position = position;
            Direction = direction;
            Normal = normal;
            Lateral = lateral;
            Arc = arc;
        }

        public Anchor(in SplinePoint point) {
            Position = point.Position;
            Direction = point.Direction;
            Normal = point.Normal;
            Lateral = point.Lateral;
            Arc = point.Arc;
        }

        public Anchor(in SplinePoint point, float3 localOffset) {
            Position = point.LocalToWorld(localOffset);
            Direction = point.Direction;
            Normal = point.Normal;
            Lateral = point.Lateral;
            Arc = point.Arc;
        }

        public quaternion Rotation => quaternion.LookRotation(Direction, -Normal);

        public static Anchor Default => new(float3.zero, math.back(), math.down(), math.right(), 0f);
    }
}
