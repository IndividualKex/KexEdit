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
            result = new CorePoint(
                heartPosition: p.HeartPosition,
                direction: p.Direction,
                normal: p.Normal,
                lateral: p.Lateral,
                velocity: p.Velocity,
                energy: p.Energy,
                normalForce: p.NormalForce,
                lateralForce: p.LateralForce,
                heartArc: p.HeartArc,
                spineArc: p.SpineArc,
                heartAdvance: p.HeartAdvance,
                frictionOrigin: p.FrictionOrigin,
                rollSpeed: p.RollSpeed,
                heartOffset: p.HeartOffset,
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

            float heartDistanceFromLast = math.distance(p.HeartPosition, prev.HeartPosition);
            float distanceFromLast = math.distance(
                p.HeartPosition + p.Normal * p.HeartOffset,
                prev.HeartPosition + prev.Normal * prev.HeartOffset
            );

            result = new PointData {
                HeartPosition = p.HeartPosition,
                Direction = p.Direction,
                Lateral = p.Lateral,
                Normal = p.Normal,
                Roll = roll,
                Velocity = p.Velocity,
                Energy = p.Energy,
                NormalForce = p.NormalForce,
                LateralForce = p.LateralForce,
                SpineAdvance = distanceFromLast,
                HeartAdvance = heartDistanceFromLast,
                AngleFromLast = angleFromLast,
                PitchFromLast = pitchFromLast,
                YawFromLast = yawFromLast,
                RollSpeed = p.RollSpeed,
                HeartArc = p.HeartArc,
                SpineArc = p.SpineArc,
                FrictionOrigin = p.FrictionOrigin,
                HeartOffset = p.HeartOffset,
                Friction = p.Friction,
                Resistance = p.Resistance,
                Facing = prev.Facing,
            };
        }

        [BurstCompile]
        public static void ToPointDataZeroAngles(in CorePoint p, in PointData anchor, out PointData result) {
            float roll = math.degrees(math.atan2(p.Lateral.y, -p.Normal.y));
            roll = (roll + 540) % 360 - 180;

            result = new PointData {
                HeartPosition = p.HeartPosition,
                Direction = p.Direction,
                Lateral = p.Lateral,
                Normal = p.Normal,
                Roll = roll,
                Velocity = p.Velocity,
                Energy = p.Energy,
                NormalForce = p.NormalForce,
                LateralForce = p.LateralForce,
                SpineAdvance = 0f,
                HeartAdvance = 0f,
                AngleFromLast = 0f,
                PitchFromLast = 0f,
                YawFromLast = 0f,
                RollSpeed = p.RollSpeed,
                HeartArc = p.HeartArc,
                SpineArc = p.SpineArc,
                FrictionOrigin = 0f,
                HeartOffset = p.HeartOffset,
                Friction = p.Friction,
                Resistance = p.Resistance,
                Facing = anchor.Facing,
            };
        }

        [BurstCompile]
        public static void ToPointDataCopyAngles(in CorePoint s, in PointData prev, out PointData result) {
            float roll = math.degrees(math.atan2(s.Lateral.y, -s.Normal.y));
            roll = (roll + 540) % 360 - 180;

            result = new PointData {
                HeartPosition = s.HeartPosition,
                Direction = s.Direction,
                Lateral = s.Lateral,
                Normal = s.Normal,
                Roll = roll,
                Velocity = s.Velocity,
                Energy = s.Energy,
                NormalForce = s.NormalForce,
                LateralForce = s.LateralForce,
                SpineAdvance = prev.SpineAdvance,
                HeartAdvance = prev.HeartAdvance,
                AngleFromLast = prev.AngleFromLast,
                PitchFromLast = prev.PitchFromLast,
                YawFromLast = prev.YawFromLast,
                RollSpeed = s.RollSpeed,
                HeartArc = s.HeartArc,
                SpineArc = s.SpineArc,
                FrictionOrigin = s.FrictionOrigin,
                HeartOffset = s.HeartOffset,
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
                result[i] = new CorePoint(
                    direction: p.Direction,
                    lateral: p.Lateral,
                    normal: p.Normal,
                    heartPosition: p.HeartPosition,
                    velocity: p.Velocity,
                    energy: p.Energy,
                    normalForce: p.NormalForce,
                    lateralForce: p.LateralForce,
                    heartArc: p.HeartArc,
                    spineArc: p.SpineArc,
                    heartAdvance: p.HeartAdvance,
                    frictionOrigin: p.FrictionOrigin,
                    rollSpeed: p.RollSpeed,
                    heartOffset: p.HeartOffset,
                    friction: p.Friction,
                    resistance: p.Resistance
                );
            }
            return result;
        }
    }
}
