using KexEdit.Core;
using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Nodes.Anchor {
    [BurstCompile]
    public static class AnchorNode {
        [BurstCompile]
        public static void Build(
            in float3 position, float pitch, float yaw, float roll,
            float velocity, float energy,
            float heartOffset, float friction, float resistance,
            out Point result
        ) {
            Frame frame = Frame.FromEuler(pitch, yaw, roll);
            result = new Point(
                spinePosition: position,
                direction: frame.Direction,
                normal: frame.Normal,
                lateral: frame.Lateral,
                velocity: velocity,
                energy: energy,
                normalForce: 1f,
                lateralForce: 0f,
                heartArc: 0f,
                spineArc: 0f,
                spineAdvance: 0f,
                frictionOrigin: 0f,
                rollSpeed: 0f,
                heartOffset: heartOffset,
                friction: friction,
                resistance: resistance
            );
        }
    }
}
