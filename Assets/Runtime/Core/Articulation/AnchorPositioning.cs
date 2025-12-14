using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Core.Articulation {
    [BurstCompile]
    public static class AnchorPositioning {
        [BurstCompile]
        public static Anchor Position(in NativeArray<SplinePoint> spline, float arc) {
            if (spline.Length < 2) return Anchor.Default;

            float startArc = SplineInterpolation.StartArc(spline);
            float endArc = SplineInterpolation.EndArc(spline);

            if (arc < startArc) {
                return ProjectBefore(spline, arc, startArc);
            }

            if (arc > endArc) {
                return ProjectAfter(spline, arc, endArc);
            }

            SplinePoint point = SplineInterpolation.Interpolate(spline, arc);
            return new Anchor(point);
        }

        [BurstCompile]
        public static Anchor Position(in NativeArray<SplinePoint> spline, float arc, float3 localOffset) {
            if (spline.Length < 2) return Anchor.Default;

            float startArc = SplineInterpolation.StartArc(spline);
            float endArc = SplineInterpolation.EndArc(spline);

            if (arc < startArc) {
                return ProjectBefore(spline, arc, startArc, localOffset);
            }

            if (arc > endArc) {
                return ProjectAfter(spline, arc, endArc, localOffset);
            }

            SplinePoint point = SplineInterpolation.Interpolate(spline, arc);
            return new Anchor(point, localOffset);
        }

        [BurstCompile]
        public static Anchor Position(in NativeArray<SplinePoint> spline, float referenceArc, in AnchorOffset offset) {
            float arc = referenceArc + offset.Arc;
            return Position(spline, arc, offset.Local);
        }

        [BurstCompile]
        private static Anchor ProjectBefore(in NativeArray<SplinePoint> spline, float arc, float startArc) {
            SplinePoint edge = spline[0];
            float overshoot = startArc - arc;
            float3 position = edge.Position - edge.Direction * overshoot;
            return new Anchor(position, edge.Direction, edge.Normal, edge.Lateral, arc);
        }

        [BurstCompile]
        private static Anchor ProjectBefore(in NativeArray<SplinePoint> spline, float arc, float startArc, float3 localOffset) {
            SplinePoint edge = spline[0];
            float overshoot = startArc - arc;
            float3 basePosition = edge.Position - edge.Direction * overshoot;
            float3 position = basePosition
                + edge.Direction * localOffset.x
                + edge.Normal * localOffset.y
                + edge.Lateral * localOffset.z;
            return new Anchor(position, edge.Direction, edge.Normal, edge.Lateral, arc);
        }

        [BurstCompile]
        private static Anchor ProjectAfter(in NativeArray<SplinePoint> spline, float arc, float endArc) {
            SplinePoint edge = spline[^1];
            float overshoot = arc - endArc;
            float3 position = edge.Position + edge.Direction * overshoot;
            return new Anchor(position, edge.Direction, edge.Normal, edge.Lateral, arc);
        }

        [BurstCompile]
        private static Anchor ProjectAfter(in NativeArray<SplinePoint> spline, float arc, float endArc, float3 localOffset) {
            SplinePoint edge = spline[^1];
            float overshoot = arc - endArc;
            float3 basePosition = edge.Position + edge.Direction * overshoot;
            float3 position = basePosition
                + edge.Direction * localOffset.x
                + edge.Normal * localOffset.y
                + edge.Lateral * localOffset.z;
            return new Anchor(position, edge.Direction, edge.Normal, edge.Lateral, arc);
        }
    }
}
