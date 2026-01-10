using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Sim {
    [BurstCompile]
    public static class Sim {
        public const float M = 1f;
        public const float G = 9.80665f;
        public const float HZ = 100f;
        public const float DT = 1f / HZ;
        public const float EPSILON = 1.192092896e-07f;
        public const float MIN_VELOCITY = 0.1f;
        public const float MAX_VELOCITY = 150f;
        public const float MAX_FORCE = 10f;
        public const float HEART_BASE = 1.1f;
        public const float FRICTION_BASE = 0.021f;
        public const float RESISTANCE_BASE = 2e-5f;

        [BurstCompile]
        public static float WrapAngle(float rad) {
            if (rad >= -math.PI && rad <= math.PI) return rad;
            const float TWO_PI = 2f * math.PI;
            const float THREE_PI = 3f * math.PI;
            return (rad + THREE_PI) % TWO_PI - math.PI;
        }

        /// <summary>
        /// Update velocity using delta-based formulation (numerically stable).
        /// Uses change in Y position rather than absolute energy to avoid
        /// catastrophic cancellation when subtracting large similar numbers.
        /// </summary>
        [BurstCompile]
        public static float UpdateVelocity(
            float prevVelocity,
            float deltaY,
            float deltaDistance,
            float friction,
            float resistance
        ) {
            float deltaPE = G * deltaY + G * friction * deltaDistance;
            float dragLoss = prevVelocity * prevVelocity * prevVelocity * resistance * DT;
            float vSquared = prevVelocity * prevVelocity - 2f * deltaPE - 2f * dragLoss;
            return math.sqrt(math.max(0f, vSquared));
        }
    }
}
