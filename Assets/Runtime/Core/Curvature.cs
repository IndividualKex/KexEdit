using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Core {
    [BurstCompile]
    public readonly struct Curvature {
        public readonly float DeltaPitch;
        public readonly float DeltaYaw;
        public readonly float YawScale;
        public readonly float TotalAngle;

        public Curvature(float deltaPitch, float deltaYaw, float yawScale, float totalAngle) {
            DeltaPitch = deltaPitch;
            DeltaYaw = deltaYaw;
            YawScale = yawScale;
            TotalAngle = totalAngle;
        }

        public static Curvature FromFrames(in Frame curr, in Frame prev) {
            float3 diff = curr.Direction - prev.Direction;
            if (math.length(diff) < Sim.EPSILON) {
                return new Curvature(0f, 0f, math.cos(math.abs(curr.Pitch)), 0f);
            }

            float deltaPitch = Sim.WrapAngle(curr.Pitch - prev.Pitch);
            float deltaYaw = Sim.WrapAngle(curr.Yaw - prev.Yaw);
            float yawScale = math.cos(math.abs(curr.Pitch));
            float totalAngle = math.sqrt(
                yawScale * yawScale * deltaYaw * deltaYaw
                + deltaPitch * deltaPitch
            );

            return new Curvature(deltaPitch, deltaYaw, yawScale, totalAngle);
        }

        public static Curvature Zero => new(0f, 0f, 1f, 0f);
    }
}
