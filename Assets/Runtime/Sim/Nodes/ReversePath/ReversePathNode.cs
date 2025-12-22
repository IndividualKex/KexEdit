using KexEdit.Sim;
using Unity.Burst;
using Unity.Collections;

namespace KexEdit.Sim.Nodes.ReversePath {
    public static class ReversePathPorts {
        public const int Path = 0;
    }

    [BurstCompile]
    public static class ReversePathNode {
        [BurstCompile]
        public static void Build(in NativeArray<Point> path, ref NativeList<Point> result) {
            result.Clear();

            if (path.Length == 0) return;

            float totalHeartArc = path[^1].HeartArc;
            float totalSpineArc = path[^1].SpineArc;

            for (int i = path.Length - 1; i >= 0; i--) {
                Point p = path[i];

                float heartAdvance = p.HeartAdvance;
                float heartArc = totalHeartArc - p.HeartArc;
                float spineArc = totalSpineArc - p.SpineArc;

                result.Add(new Point(
                    heartPosition: p.HeartPosition,
                    direction: -p.Direction,
                    normal: p.Normal,
                    lateral: -p.Lateral,
                    velocity: p.Velocity,
                    normalForce: p.NormalForce,
                    lateralForce: -p.LateralForce,
                    heartArc: heartArc,
                    spineArc: spineArc,
                    heartAdvance: heartAdvance,
                    frictionOrigin: 0f,
                    rollSpeed: p.RollSpeed,
                    heartOffset: p.HeartOffset,
                    friction: p.Friction,
                    resistance: p.Resistance
                ));
            }
        }
    }
}
