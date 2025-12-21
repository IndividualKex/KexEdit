using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Legacy.Constants;
using CoreKeyframe = KexEdit.Core.Keyframe;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Legacy {
    [BurstCompile]
    public static class PointConverter {
        [BurstCompile]
        public static void ToPoint(in PointData p, out CorePoint result) {
            // Legacy PointData uses inverted naming: Position = heart, TotalHeartLength = spine
            result = new CorePoint(
                heartPosition: p.Position,
                direction: p.Direction,
                normal: p.Normal,
                lateral: p.Lateral,
                velocity: p.Velocity,
                energy: p.Energy,
                normalForce: p.NormalForce,
                lateralForce: p.LateralForce,
                heartArc: p.TotalLength,
                spineArc: p.TotalHeartLength,
                heartAdvance: p.HeartDistanceFromLast,
                frictionOrigin: p.FrictionCompensation,
                rollSpeed: p.RollSpeed,
                heartOffset: p.Heart,
                friction: p.Friction,
                resistance: p.Resistance
            );
        }

        [BurstCompile]
        public static void ToPointData(in CorePoint p, in PointData prev, out PointData result) {
            float roll = math.degrees(math.atan2(p.Lateral.y, -p.Normal.y));
            roll = (roll + 540) % 360 - 180;

            float pitch = GetPitch(in p.Direction);
            float yaw = GetYaw(in p.Direction);
            float prevPitch = GetPitch(in prev.Direction);
            float prevYaw = GetYaw(in prev.Direction);

            float pitchFromLast = 0f;
            float yawFromLast = 0f;
            float3 diff = p.Direction - prev.Direction;
            if (math.length(diff) >= EPSILON) {
                pitchFromLast = (pitch - prevPitch + 540) % 360 - 180;
                yawFromLast = (yaw - prevYaw + 540) % 360 - 180;
            }

            float yawScaleFactor = math.cos(math.abs(math.radians(pitch)));
            float angleFromLast = math.sqrt(
                yawScaleFactor * yawScaleFactor * yawFromLast * yawFromLast
                + pitchFromLast * pitchFromLast
            );

            float heartDistanceFromLast = math.distance(p.HeartPosition, prev.Position);
            float distanceFromLast = math.distance(
                p.HeartPosition + p.Normal * p.HeartOffset,
                prev.Position + prev.Normal * prev.Heart
            );

            // Legacy PointData uses inverted naming: Position = heart, TotalHeartLength = spine
            result = new PointData {
                Position = p.HeartPosition,
                Direction = p.Direction,
                Lateral = p.Lateral,
                Normal = p.Normal,
                Roll = roll,
                Velocity = p.Velocity,
                Energy = p.Energy,
                NormalForce = p.NormalForce,
                LateralForce = p.LateralForce,
                DistanceFromLast = distanceFromLast,
                HeartDistanceFromLast = heartDistanceFromLast,
                AngleFromLast = angleFromLast,
                PitchFromLast = pitchFromLast,
                YawFromLast = yawFromLast,
                RollSpeed = p.RollSpeed,
                TotalLength = p.HeartArc,
                TotalHeartLength = p.SpineArc,
                FrictionCompensation = p.FrictionOrigin,
                Heart = p.HeartOffset,
                Friction = p.Friction,
                Resistance = p.Resistance,
                Facing = prev.Facing,
            };
        }

        [BurstCompile]
        public static void ToPointDataZeroAngles(in CorePoint p, in PointData anchor, out PointData result) {
            float roll = math.degrees(math.atan2(p.Lateral.y, -p.Normal.y));
            roll = (roll + 540) % 360 - 180;

            // Legacy PointData uses inverted naming: Position = heart, TotalHeartLength = spine
            result = new PointData {
                Position = p.HeartPosition,
                Direction = p.Direction,
                Lateral = p.Lateral,
                Normal = p.Normal,
                Roll = roll,
                Velocity = p.Velocity,
                Energy = p.Energy,
                NormalForce = p.NormalForce,
                LateralForce = p.LateralForce,
                DistanceFromLast = 0f,
                HeartDistanceFromLast = 0f,
                AngleFromLast = 0f,
                PitchFromLast = 0f,
                YawFromLast = 0f,
                RollSpeed = p.RollSpeed,
                TotalLength = p.HeartArc,
                TotalHeartLength = p.SpineArc,
                FrictionCompensation = 0f,
                Heart = p.HeartOffset,
                Friction = p.Friction,
                Resistance = p.Resistance,
                Facing = anchor.Facing,
            };
        }

        [BurstCompile]
        public static void ToPointDataCopyAngles(in CorePoint s, in PointData prev, out PointData result) {
            float roll = math.degrees(math.atan2(s.Lateral.y, -s.Normal.y));
            roll = (roll + 540) % 360 - 180;

            // Legacy PointData uses inverted naming: Position = heart, TotalHeartLength = spine
            result = new PointData {
                Position = s.HeartPosition,
                Direction = s.Direction,
                Lateral = s.Lateral,
                Normal = s.Normal,
                Roll = roll,
                Velocity = s.Velocity,
                Energy = s.Energy,
                NormalForce = s.NormalForce,
                LateralForce = s.LateralForce,
                DistanceFromLast = prev.DistanceFromLast,
                HeartDistanceFromLast = prev.HeartDistanceFromLast,
                AngleFromLast = prev.AngleFromLast,
                PitchFromLast = prev.PitchFromLast,
                YawFromLast = prev.YawFromLast,
                RollSpeed = s.RollSpeed,
                TotalLength = s.HeartArc,
                TotalHeartLength = s.SpineArc,
                FrictionCompensation = s.FrictionOrigin,
                Heart = s.HeartOffset,
                Friction = s.Friction,
                Resistance = s.Resistance,
                Facing = prev.Facing,
            };
        }

        [BurstCompile]
        public static float GetPitch(in float3 direction) {
            float magnitude = math.sqrt(direction.x * direction.x + direction.z * direction.z);
            return math.degrees(math.atan2(direction.y, magnitude));
        }

        [BurstCompile]
        public static float GetYaw(in float3 direction) {
            return math.degrees(math.atan2(-direction.x, -direction.z));
        }

        public static NativeArray<CoreKeyframe> ConvertKeyframes<T>(DynamicBuffer<T> buffer, Allocator allocator)
            where T : unmanaged, IBufferElementData {
            var result = new NativeArray<CoreKeyframe>(buffer.Length, allocator);
            for (int i = 0; i < buffer.Length; i++) {
                var legacy = buffer.Reinterpret<Keyframe>()[i];
                result[i] = new CoreKeyframe(
                    legacy.Time,
                    legacy.Value,
                    (Core.InterpolationType)(int)legacy.InInterpolation,
                    (Core.InterpolationType)(int)legacy.OutInterpolation,
                    legacy.InTangent,
                    legacy.OutTangent,
                    legacy.InWeight,
                    legacy.OutWeight
                );
            }
            return result;
        }

        public static NativeArray<CorePoint> ConvertPathBuffer(ref DynamicBuffer<PathPort> buffer, Allocator allocator) {
            var result = new NativeArray<CorePoint>(buffer.Length, allocator);
            for (int i = 0; i < buffer.Length; i++) {
                PointData p = buffer[i].Value;
                // Legacy PointData uses inverted naming: Position = heart, TotalHeartLength = spine
                result[i] = new CorePoint(
                    direction: p.Direction,
                    lateral: p.Lateral,
                    normal: p.Normal,
                    heartPosition: p.Position,
                    velocity: p.Velocity,
                    energy: p.Energy,
                    normalForce: p.NormalForce,
                    lateralForce: p.LateralForce,
                    heartArc: p.TotalLength,
                    spineArc: p.TotalHeartLength,
                    heartAdvance: p.HeartDistanceFromLast,
                    frictionOrigin: p.FrictionCompensation,
                    rollSpeed: p.RollSpeed,
                    heartOffset: p.Heart,
                    friction: p.Friction,
                    resistance: p.Resistance
                );
            }
            return result;
        }
    }
}
