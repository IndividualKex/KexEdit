using KexEdit.Core;
using Unity.Burst;
using Unity.Collections;

namespace KexEdit.Nodes.ReversePath {
    [BurstCompile]
    public static class ReversePathNode {
        [BurstCompile]
        public static void Build(in NativeArray<Point> path, ref NativeList<Point> result) {
            result.Clear();

            if (path.Length == 0) return;

            for (int i = path.Length - 1; i >= 0; i--) {
                Point p = path[i];
                result.Add(new Point(
                    spinePosition: p.SpinePosition,
                    direction: -p.Direction,
                    normal: p.Normal,
                    lateral: -p.Lateral,
                    velocity: p.Velocity,
                    energy: p.Energy,
                    normalForce: p.NormalForce,
                    lateralForce: -p.LateralForce,
                    heartArc: p.HeartArc,
                    spineArc: p.SpineArc,
                    spineAdvance: p.SpineAdvance,
                    frictionOrigin: p.FrictionOrigin,
                    rollSpeed: p.RollSpeed,
                    heartOffset: p.HeartOffset,
                    friction: p.Friction,
                    resistance: p.Resistance
                ));
            }
        }
    }
}
