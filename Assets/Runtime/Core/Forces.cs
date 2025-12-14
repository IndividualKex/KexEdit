using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Core {
    [BurstCompile]
    public readonly struct Forces {
        public readonly float Normal;
        public readonly float Lateral;

        public Forces(float normal, float lateral) {
            Normal = normal;
            Lateral = lateral;
        }

        public static Forces Compute(
            in Curvature curvature,
            in Frame frame,
            float velocity,
            float spineAdvance
        ) {
            if (math.abs(curvature.TotalAngle) < Sim.EPSILON) {
                return new Forces(
                    -math.dot(math.up(), frame.Normal),
                    -math.dot(math.up(), frame.Lateral)
                );
            }

            float cosRoll = math.cos(frame.Roll);
            float sinRoll = math.sin(frame.Roll);

            float normalAngle = -curvature.DeltaPitch * cosRoll
                              - curvature.YawScale * curvature.DeltaYaw * sinRoll;
            float lateralAngle = curvature.DeltaPitch * sinRoll
                               - curvature.YawScale * curvature.DeltaYaw * cosRoll;

            float3 forceVec = math.up()
                + velocity * Sim.HZ * lateralAngle * frame.Lateral / Sim.G
                + spineAdvance * Sim.HZ * Sim.HZ * normalAngle * frame.Normal / Sim.G;

            return new Forces(
                -math.dot(forceVec, frame.Normal),
                -math.dot(forceVec, frame.Lateral)
            );
        }

        public static Forces OneG => new(1f, 0f);
        public static Forces Zero => new(0f, 0f);
    }
}
