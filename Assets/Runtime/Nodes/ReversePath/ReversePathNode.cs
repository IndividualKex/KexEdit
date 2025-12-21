using KexEdit.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Nodes.ReversePath {
    [BurstCompile]
    public static class ReversePathNode {
        [BurstCompile]
        public static void Build(in NativeArray<Point> path, ref NativeList<Point> result) {
            result.Clear();

            if (path.Length == 0) return;

            float heartArc = 0f;
            float spineArc = 0f;

            for (int i = path.Length - 1; i >= 0; i--) {
                Point p = path[i];

                float heartAdvance = 0f;
                if (i > 0) {
                    Point next = path[i - 1];
                    heartAdvance = math.distance(p.HeartPosition, next.HeartPosition);
                }

                result.Add(new Point(
                    heartPosition: p.HeartPosition,
                    direction: -p.Direction,
                    normal: p.Normal,
                    lateral: -p.Lateral,
                    velocity: p.Velocity,
                    energy: p.Energy,
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

                heartArc += heartAdvance;
                spineArc += heartAdvance;
            }
        }
    }
}
