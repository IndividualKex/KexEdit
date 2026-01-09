using Unity.Burst;
using Unity.Mathematics;
using static KexEdit.Sim.Sim;

namespace KexEdit.Legacy {
    [BurstCompile]
    public static class CorePointBufferExtensions {
        public static float3 HeartPosition(this in CorePointBuffer p) => p.Point.HeartPosition;
        public static float3 Direction(this in CorePointBuffer p) => p.Point.Direction;
        public static float3 Lateral(this in CorePointBuffer p) => p.Point.Lateral;
        public static float3 Normal(this in CorePointBuffer p) => p.Point.Normal;

        public static float Roll(this in CorePointBuffer p) {
            float roll = math.degrees(p.Point.Roll);
            return (roll + 540f) % 360f - 180f;
        }

        public static float Velocity(this in CorePointBuffer p) => p.Point.Velocity;
        public static float NormalForce(this in CorePointBuffer p) => p.Point.NormalForce;
        public static float LateralForce(this in CorePointBuffer p) => p.Point.LateralForce;
        public static float RollSpeed(this in CorePointBuffer p) => p.Point.RollSpeed;

        public static float HeartArc(this in CorePointBuffer p) => p.Point.HeartArc;
        public static float SpineArc(this in CorePointBuffer p) => p.Point.SpineArc;
        public static float SpineAdvance(this in CorePointBuffer p) => p.SpineDistanceFromLast;
        public static float HeartAdvance(this in CorePointBuffer p) => p.DistanceFromLast;
        public static float FrictionOrigin(this in CorePointBuffer p) => p.Point.FrictionOrigin;

        public static float HeartOffset(this in CorePointBuffer p) => p.Point.HeartOffset;
        public static float Friction(this in CorePointBuffer p) => p.Point.Friction;
        public static float Resistance(this in CorePointBuffer p) => p.Point.Resistance;

        public static float GetPitch(this in CorePointBuffer p) =>
            PointConverter.GetPitch(in p.Point.Direction);

        public static float GetYaw(this in CorePointBuffer p) =>
            PointConverter.GetYaw(in p.Point.Direction);

        public static float GetCenter(this in CorePointBuffer p) => p.Point.HeartOffset * 0.9f;

        public static float3 GetSpinePosition(this in CorePointBuffer p, float heartOffset) {
            return p.Point.HeartPosition + p.Point.Normal * heartOffset;
        }

        public static float ComputeEnergy(this in CorePointBuffer p) {
            float3 centerPos = p.GetSpinePosition(p.GetCenter());
            float energy = 0.5f * p.Point.Velocity * p.Point.Velocity + G * centerPos.y;
            energy += G * (p.Point.HeartArc - p.Point.FrictionOrigin) * p.Point.Friction;
            return energy;
        }

        public static float3 GetSpineDirection(this in CorePointBuffer p, float heartOffset) {
            float dist = p.AngleFromLast < 1e-3f ? p.SpineDistanceFromLast : p.Point.Velocity / HZ;
            float rollSpeed = dist > 0f ? p.Point.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) rollSpeed = 0f;
            float3 deviation = p.Point.Lateral * math.radians(rollSpeed * heartOffset);
            return math.normalize(p.Point.Direction + deviation);
        }

        public static float3 GetSpineLateral(this in CorePointBuffer p, float heartOffset) {
            float dist = p.AngleFromLast < 1e-3f ? p.SpineDistanceFromLast : p.Point.Velocity / HZ;
            float rollSpeed = dist > 0f ? p.Point.RollSpeed / HZ / dist : 0f;
            if (float.IsNaN(rollSpeed)) rollSpeed = 0f;
            float3 deviation = -p.Point.Direction * math.radians(rollSpeed * heartOffset);
            return math.normalize(p.Point.Lateral + deviation);
        }

        public static PointData ToPointData(this in CorePointBuffer p) {
            return new PointData {
                HeartPosition = p.Point.HeartPosition,
                Direction = p.Point.Direction,
                Lateral = p.Point.Lateral,
                Normal = p.Point.Normal,
                Roll = p.Roll(),
                Velocity = p.Point.Velocity,
                Energy = 0f, // Energy no longer used in modern system
                NormalForce = p.Point.NormalForce,
                LateralForce = p.Point.LateralForce,
                SpineAdvance = p.DistanceFromLast,
                HeartAdvance = p.SpineDistanceFromLast,
                AngleFromLast = p.AngleFromLast,
                PitchFromLast = p.PitchFromLast,
                YawFromLast = p.YawFromLast,
                RollSpeed = p.Point.RollSpeed,
                HeartArc = p.Point.HeartArc,
                SpineArc = p.Point.SpineArc,
                FrictionOrigin = p.Point.FrictionOrigin,
                HeartOffset = p.Point.HeartOffset,
                Friction = p.Point.Friction,
                Resistance = p.Point.Resistance,
                Facing = p.Facing
            };
        }

        public static CorePointBuffer Lerp(in CorePointBuffer p0, in CorePointBuffer p1, float t) {
            PointData pd0 = p0.ToPointData();
            PointData pd1 = p1.ToPointData();
            PointData result = PointData.Lerp(pd0, pd1, t);
            PointConverter.ToPoint(in result, out var corePoint);
            CorePointBuffer.Create(in corePoint, in p0.Point, p0.Facing, out CorePointBuffer buffer);
            return buffer;
        }
    }
}

