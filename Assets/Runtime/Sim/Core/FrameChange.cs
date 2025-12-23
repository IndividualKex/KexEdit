using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Sim {
    [BurstCompile]
    public readonly struct FrameChange {
        public readonly float3 NewDirection;
        public readonly float3 NewNormal;
        public readonly float3 NewLateral;

        public FrameChange(in float3 newDirection, in float3 newNormal, in float3 newLateral) {
            NewDirection = newDirection;
            NewNormal = newNormal;
            NewLateral = newLateral;
        }

        [BurstCompile]
        public static void FromAngles(in Frame prev, float deltaPitch, float deltaYaw, out FrameChange result) {
            Frame pitched = prev.WithPitch(deltaPitch);
            Frame yawed = pitched.WithYaw(deltaYaw);
            result = new FrameChange(yawed.Direction, yawed.Normal, yawed.Lateral);
        }

        [BurstCompile]
        public static void FromAxis(in Frame prev, in float3 axis, float angle, out FrameChange result) {
            Frame rotated = prev.RotateAround(axis, angle);
            result = new FrameChange(rotated.Direction, rotated.Normal, rotated.Lateral);
        }
    }
}
