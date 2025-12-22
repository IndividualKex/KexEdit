using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Core {
    [BurstCompile]
    public static class Sim {
        public const float G = 9.80665f;
        public const float HZ = 100f;
        public const float DT = 1f / HZ;
        public const float EPSILON = 1.192092896e-07f;
        public const float MIN_VELOCITY = 1e-3f;

        [BurstCompile]
        public static float WrapAngle(float rad) {
            if (rad >= -math.PI && rad <= math.PI) return rad;
            const float TWO_PI = 2f * math.PI;
            const float THREE_PI = 3f * math.PI;
            return (rad + THREE_PI) % TWO_PI - math.PI;
        }

        [BurstCompile]
        public static float ComputeTotalEnergy(float velocity, float centerY, float frictionDistance, float friction) {
            return 0.5f * velocity * velocity + G * centerY + G * frictionDistance * friction;
        }

        [BurstCompile]
        public static void UpdateEnergy(
            float prevEnergy,
            float prevVelocity,
            float centerY,
            float frictionDistance,
            float friction,
            float resistance,
            out float newEnergy,
            out float newVelocity
        ) {
            float pe = G * (centerY + frictionDistance * friction);
            newEnergy = prevEnergy - prevVelocity * prevVelocity * prevVelocity * resistance * DT;
            newVelocity = math.sqrt(2f * math.max(0f, newEnergy - pe));
        }
    }
}
