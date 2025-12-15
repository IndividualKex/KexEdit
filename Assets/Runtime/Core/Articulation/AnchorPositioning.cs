using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Core.Articulation {
    [BurstCompile]
    public static class AnchorPositioning {
        [BurstCompile]
        public static void Position(in NativeArray<SplinePoint> spline, float arc, out Anchor result) {
            if (spline.Length < 2) {
                result = Anchor.Default;
                return;
            }

            float startArc = SplineInterpolation.StartArc(spline);
            float endArc = SplineInterpolation.EndArc(spline);

            if (arc < startArc) {
                ProjectBefore(spline, arc, startArc, out result);
                return;
            }

            if (arc > endArc) {
                ProjectAfter(spline, arc, endArc, out result);
                return;
            }

            SplineInterpolation.Interpolate(spline, arc, out SplinePoint point);
            result = new Anchor(point);
        }

        [BurstCompile]
        public static void Position(in NativeArray<SplinePoint> spline, float arc, in float3 localOffset, out Anchor result) {
            if (spline.Length < 2) {
                result = Anchor.Default;
                return;
            }

            float startArc = SplineInterpolation.StartArc(spline);
            float endArc = SplineInterpolation.EndArc(spline);

            if (arc < startArc) {
                ProjectBefore(spline, arc, startArc, localOffset, out result);
                return;
            }

            if (arc > endArc) {
                ProjectAfter(spline, arc, endArc, localOffset, out result);
                return;
            }

            SplineInterpolation.Interpolate(spline, arc, out SplinePoint point);
            result = new Anchor(point, localOffset);
        }

        [BurstCompile]
        public static void Position(in NativeArray<SplinePoint> spline, float referenceArc, in AnchorOffset offset, out Anchor result) {
            float arc = referenceArc + offset.Arc;
            Position(spline, arc, offset.Local, out result);
        }

        [BurstCompile]
        private static void ProjectBefore(in NativeArray<SplinePoint> spline, float arc, float startArc, out Anchor result) {
            SplinePoint edge = spline[0];
            float overshoot = startArc - arc;
            float3 position = edge.Position - edge.Direction * overshoot;
            result = new Anchor(position, edge.Direction, edge.Normal, edge.Lateral, arc);
        }

        [BurstCompile]
        private static void ProjectBefore(in NativeArray<SplinePoint> spline, float arc, float startArc, in float3 localOffset, out Anchor result) {
            SplinePoint edge = spline[0];
            float overshoot = startArc - arc;
            float3 basePosition = edge.Position - edge.Direction * overshoot;
            float3 position = basePosition
                + edge.Direction * localOffset.x
                + edge.Normal * localOffset.y
                + edge.Lateral * localOffset.z;
            result = new Anchor(position, edge.Direction, edge.Normal, edge.Lateral, arc);
        }

        [BurstCompile]
        private static void ProjectAfter(in NativeArray<SplinePoint> spline, float arc, float endArc, out Anchor result) {
            SplinePoint edge = spline[^1];
            float overshoot = arc - endArc;
            float3 position = edge.Position + edge.Direction * overshoot;
            result = new Anchor(position, edge.Direction, edge.Normal, edge.Lateral, arc);
        }

        [BurstCompile]
        private static void ProjectAfter(in NativeArray<SplinePoint> spline, float arc, float endArc, in float3 localOffset, out Anchor result) {
            SplinePoint edge = spline[^1];
            float overshoot = arc - endArc;
            float3 basePosition = edge.Position + edge.Direction * overshoot;
            float3 position = basePosition
                + edge.Direction * localOffset.x
                + edge.Normal * localOffset.y
                + edge.Lateral * localOffset.z;
            result = new Anchor(position, edge.Direction, edge.Normal, edge.Lateral, arc);
        }
    }
}
