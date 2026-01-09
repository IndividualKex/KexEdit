using KexEdit.Sim;
using Unity.Burst;

namespace KexEdit.Sim.Nodes.Reverse {
    public static class ReversePorts {
        public const int Anchor = 0;
    }

    [BurstCompile]
    public static class ReverseNode {
        [BurstCompile]
        public static void Build(in Point anchor, out Point result) {
            result = new Point(
                heartPosition: anchor.HeartPosition,
                direction: -anchor.Direction,
                normal: anchor.Normal,
                lateral: -anchor.Lateral,
                velocity: anchor.Velocity,
                normalForce: anchor.NormalForce,
                lateralForce: anchor.LateralForce,
                heartArc: anchor.HeartArc,
                spineArc: anchor.SpineArc,
                heartAdvance: anchor.HeartAdvance,
                frictionOrigin: anchor.FrictionOrigin,
                rollSpeed: anchor.RollSpeed,
                heartOffset: anchor.HeartOffset,
                friction: anchor.Friction,
                resistance: anchor.Resistance
            );
        }
    }
}
