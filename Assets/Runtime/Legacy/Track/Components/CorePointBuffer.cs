using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Legacy {
    [BurstCompile]
    public struct CorePointBuffer : IBufferElementData {
        public CorePoint Point;

        public float DistanceFromLast;
        public float SpineDistanceFromLast;
        public float PitchFromLast;
        public float YawFromLast;
        public float AngleFromLast;
        public int Facing;

        [BurstCompile]
        public static CorePointBuffer Create(in CorePoint point, in CorePoint prev, int facing) {
            float3 heartPos = point.SpinePosition + point.Normal * point.HeartOffset;
            float3 prevHeartPos = prev.SpinePosition + prev.Normal * prev.HeartOffset;

            float pitchFromLast = 0f;
            float yawFromLast = 0f;
            float3 diff = point.Direction - prev.Direction;

            if (math.length(diff) >= Constants.EPSILON) {
                float pitch = PointConverter.GetPitch(in point.Direction);
                float yaw = PointConverter.GetYaw(in point.Direction);
                float prevPitch = PointConverter.GetPitch(in prev.Direction);
                float prevYaw = PointConverter.GetYaw(in prev.Direction);

                pitchFromLast = (pitch - prevPitch + 540f) % 360f - 180f;
                yawFromLast = (yaw - prevYaw + 540f) % 360f - 180f;
            }

            float yawScaleFactor = math.cos(math.abs(math.radians(PointConverter.GetPitch(in point.Direction))));
            float angleFromLast = math.sqrt(
                yawScaleFactor * yawScaleFactor * yawFromLast * yawFromLast
                + pitchFromLast * pitchFromLast
            );

            return new CorePointBuffer {
                Point = point,
                DistanceFromLast = math.distance(heartPos, prevHeartPos),
                SpineDistanceFromLast = math.distance(point.SpinePosition, prev.SpinePosition),
                PitchFromLast = pitchFromLast,
                YawFromLast = yawFromLast,
                AngleFromLast = angleFromLast,
                Facing = facing
            };
        }

        [BurstCompile]
        public static CorePointBuffer CreateFirst(in CorePoint point, int facing) {
            return new CorePointBuffer {
                Point = point,
                DistanceFromLast = 0f,
                SpineDistanceFromLast = 0f,
                PitchFromLast = 0f,
                YawFromLast = 0f,
                AngleFromLast = 0f,
                Facing = facing
            };
        }

    }
}
