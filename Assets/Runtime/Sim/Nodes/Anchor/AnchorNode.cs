using KexEdit.Sim;
using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Sim.Nodes.Anchor {
    public static class AnchorPorts {
        public const int Position = 0;
        public const int Roll = 1;
        public const int Pitch = 2;
        public const int Yaw = 3;
        public const int Velocity = 4;
        public const int Heart = 5;
        public const int Friction = 6;
        public const int Resistance = 7;
    }

    [BurstCompile]
    public static class AnchorNode {
        [BurstCompile]
        public static void Build(
            in float3 position, float pitch, float yaw, float roll,
            float velocity,
            float heartOffset, float friction, float resistance,
            out Point result
        ) {
            Frame frame = Frame.FromEuler(pitch, yaw, roll);
            result = new Point(
                heartPosition: position,
                direction: frame.Direction,
                normal: frame.Normal,
                lateral: frame.Lateral,
                velocity: velocity,
                normalForce: 1f,
                lateralForce: 0f,
                heartArc: 0f,
                spineArc: 0f,
                heartAdvance: 0f,
                frictionOrigin: 0f,
                rollSpeed: 0f,
                heartOffset: heartOffset,
                friction: friction,
                resistance: resistance
            );
        }
    }
}
