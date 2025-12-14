using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Core.Articulation {
    [BurstCompile]
    public readonly struct SplinePoint {
        public readonly float Arc;
        public readonly float3 Position;
        public readonly float3 Direction;
        public readonly float3 Normal;
        public readonly float3 Lateral;

        public SplinePoint(float arc, float3 position, float3 direction, float3 normal, float3 lateral) {
            Arc = arc;
            Position = position;
            Direction = direction;
            Normal = normal;
            Lateral = lateral;
        }

        [BurstCompile]
        public static SplinePoint Lerp(in SplinePoint a, in SplinePoint b, float t) {
            float arc = math.lerp(a.Arc, b.Arc, t);
            float3 position = math.lerp(a.Position, b.Position, t);
            float3 direction = math.normalize(math.lerp(a.Direction, b.Direction, t));
            float3 lateral = math.normalize(math.lerp(a.Lateral, b.Lateral, t));
            float3 normal = math.normalize(math.cross(direction, lateral));
            return new SplinePoint(arc, position, direction, normal, lateral);
        }

        public float3 LocalToWorld(float3 local) =>
            Position + Direction * local.x + Normal * local.y + Lateral * local.z;

        public static SplinePoint Default => new(0f, float3.zero, math.back(), math.down(), math.right());
    }
}
