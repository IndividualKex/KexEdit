using Unity.Burst;
using Unity.Mathematics;
using static KexEdit.Legacy.Constants;

namespace KexEdit.Legacy {
    [BurstCompile]
    public static class CorePointBufferExtensions {
        public static float3 Position(this in CorePointBuffer p) => p.Point.SpinePosition;
        public static float3 Direction(this in CorePointBuffer p) => p.Point.Direction;
        public static float3 Lateral(this in CorePointBuffer p) => p.Point.Lateral;
        public static float3 Normal(this in CorePointBuffer p) => p.Point.Normal;

        public static float Roll(this in CorePointBuffer p) {
            float roll = math.degrees(p.Point.Roll);
            return (roll + 540f) % 360f - 180f;
        }

        public static float Velocity(this in CorePointBuffer p) => p.Point.Velocity;
        public static float Energy(this in CorePointBuffer p) => p.Point.Energy;
        public static float NormalForce(this in CorePointBuffer p) => p.Point.NormalForce;
        public static float LateralForce(this in CorePointBuffer p) => p.Point.LateralForce;
        public static float RollSpeed(this in CorePointBuffer p) => p.Point.RollSpeed;

        public static float TotalLength(this in CorePointBuffer p) => p.Point.HeartArc;
        public static float TotalHeartLength(this in CorePointBuffer p) => p.Point.SpineArc;
        public static float HeartDistanceFromLast(this in CorePointBuffer p) => p.SpineDistanceFromLast;
        public static float FrictionCompensation(this in CorePointBuffer p) => p.Point.FrictionOrigin;

        public static float Heart(this in CorePointBuffer p) => p.Point.HeartOffset;
        public static float Friction(this in CorePointBuffer p) => p.Point.Friction;
        public static float Resistance(this in CorePointBuffer p) => p.Point.Resistance;

        public static float GetPitch(this in CorePointBuffer p) =>
            PointConverter.GetPitch(in p.Point.Direction);

        public static float GetYaw(this in CorePointBuffer p) =>
            PointConverter.GetYaw(in p.Point.Direction);

        public static float GetCenter(this in CorePointBuffer p) => p.Point.HeartOffset * 0.9f;

        public static float3 GetHeartPosition(this in CorePointBuffer p, float heart) {
            return p.Point.SpinePosition + p.Point.Normal * heart;
        }

        public static float ComputeEnergy(this in CorePointBuffer p) {
            float3 centerPos = p.GetHeartPosition(p.GetCenter());
            float energy = 0.5f * p.Point.Velocity * p.Point.Velocity + G * centerPos.y;
            energy += G * (p.Point.HeartArc - p.Point.FrictionOrigin) * p.Point.Friction;
            return energy;
        }

        public static float3 GetHeartDirection(this in CorePointBuffer p, float heart) {
            float dist = p.AngleFromLast < 1e-3f ? p.SpineDistanceFromLast : p.Point.Velocity / HZ;
            float rollSpeed = dist > 0f ? p.Point.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) rollSpeed = 0f;
            float3 deviation = p.Point.Lateral * math.radians(rollSpeed * heart);
            return math.normalize(p.Point.Direction + deviation);
        }

        public static float3 GetHeartLateral(this in CorePointBuffer p, float heart) {
            float dist = p.AngleFromLast < 1e-3f ? p.SpineDistanceFromLast : p.Point.Velocity / HZ;
            float rollSpeed = dist > 0f ? p.Point.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) rollSpeed = 0f;
            float3 deviation = -p.Point.Direction * math.radians(rollSpeed * heart);
            return math.normalize(p.Point.Lateral + deviation);
        }

        public static PointData ToPointData(this in CorePointBuffer p) {
            return new PointData {
                Position = p.Point.SpinePosition,
                Direction = p.Point.Direction,
                Lateral = p.Point.Lateral,
                Normal = p.Point.Normal,
                Roll = p.Roll(),
                Velocity = p.Point.Velocity,
                Energy = p.Point.Energy,
                NormalForce = p.Point.NormalForce,
                LateralForce = p.Point.LateralForce,
                DistanceFromLast = p.DistanceFromLast,
                HeartDistanceFromLast = p.SpineDistanceFromLast,
                AngleFromLast = p.AngleFromLast,
                PitchFromLast = p.PitchFromLast,
                YawFromLast = p.YawFromLast,
                RollSpeed = p.Point.RollSpeed,
                TotalLength = p.Point.HeartArc,
                TotalHeartLength = p.Point.SpineArc,
                FrictionCompensation = p.Point.FrictionOrigin,
                Heart = p.Point.HeartOffset,
                Friction = p.Point.Friction,
                Resistance = p.Point.Resistance,
                Facing = p.Facing
            };
        }
    }
}
