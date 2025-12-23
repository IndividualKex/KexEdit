using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Sim.Sim;
using CorePoint = KexEdit.Sim.Point;

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
        public static void Create(in CorePoint point, in CorePoint prev, int facing, out CorePointBuffer result) {
            float3 spinePos = point.SpinePosition(point.HeartOffset);
            float3 prevSpinePos = prev.SpinePosition(prev.HeartOffset);

            float pitchFromLast = 0f;
            float yawFromLast = 0f;
            float3 diff = point.Direction - prev.Direction;

            if (math.length(diff) >= EPSILON) {
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

            result = new CorePointBuffer {
                Point = point,
                DistanceFromLast = math.distance(spinePos, prevSpinePos),
                SpineDistanceFromLast = math.distance(point.HeartPosition, prev.HeartPosition),
                PitchFromLast = pitchFromLast,
                YawFromLast = yawFromLast,
                AngleFromLast = angleFromLast,
                Facing = facing
            };
        }

        [BurstCompile]
        public static void CreateFirst(in CorePoint point, int facing, out CorePointBuffer result) {
            result = new CorePointBuffer {
                Point = point,
                DistanceFromLast = 0f,
                SpineDistanceFromLast = 0f,
                PitchFromLast = 0f,
                YawFromLast = 0f,
                AngleFromLast = 0f,
                Facing = facing
            };
        }

        [BurstCompile]
        public static void CreateFromAnchor(in CorePoint point, in PointData anchor, out CorePointBuffer result) {
            result = new CorePointBuffer {
                Point = point,
                DistanceFromLast = anchor.SpineAdvance,
                SpineDistanceFromLast = anchor.HeartAdvance,
                PitchFromLast = anchor.PitchFromLast,
                YawFromLast = anchor.YawFromLast,
                AngleFromLast = anchor.AngleFromLast,
                Facing = anchor.Facing
            };
        }

    }
}
