using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using KexEdit.Sim;

namespace KexEdit.Spline.Resampling {
    [BurstCompile]
    public static class SplineResampler {
        [BurstCompile]
        public static void Resample(in NativeArray<Point> points, ref NativeList<SplinePoint> output) {
            output.Clear();
            if (points.Length == 0) return;

            for (int i = 0; i < points.Length; i++) {
                Point p = points[i];
                ToSplinePoint(in p, out var sp);
                output.Add(sp);
            }
        }

        [BurstCompile]
        public static void Resample(
            in NativeArray<Point> points,
            float resolution,
            ref NativeList<SplinePoint> output
        ) {
            output.Clear();
            if (points.Length == 0) return;
            if (points.Length == 1) {
                Point p = points[0];
                ToSplinePoint(in p, out var sp);
                output.Add(sp);
                return;
            }

            float startArc = points[0].SpineArc;
            float endArc = points[^1].SpineArc;
            float totalLength = endArc - startArc;

            if (totalLength <= 0f) {
                Point p = points[0];
                ToSplinePoint(in p, out var sp);
                output.Add(sp);
                return;
            }

            int numSamples = math.max(2, (int)math.ceil(totalLength / resolution) + 1);

            for (int i = 0; i < numSamples; i++) {
                float t = i / (float)(numSamples - 1);
                float targetArc = startArc + t * totalLength;
                InterpolateAtArc(points, targetArc, out SplinePoint sp);
                output.Add(sp);
            }
        }

        [BurstCompile]
        public static void ToSplinePoint(in Point point, out SplinePoint result) {
            result = new SplinePoint(
                point.SpineArc,
                point.SpinePosition(point.HeartOffset),
                point.Direction,
                point.Normal,
                point.Lateral
            );
        }

        [BurstCompile]
        private static void InterpolateAtArc(
            in NativeArray<Point> points,
            float arc,
            out SplinePoint result
        ) {
            int lo = 0;
            int hi = points.Length - 1;

            if (arc <= points[0].SpineArc) {
                Point p = points[0];
                ToSplinePoint(in p, out result);
                result = new SplinePoint(arc, result.Position, result.Direction, result.Normal, result.Lateral);
                return;
            }
            if (arc >= points[points.Length - 1].SpineArc) {
                Point p = points[points.Length - 1];
                ToSplinePoint(in p, out result);
                result = new SplinePoint(arc, result.Position, result.Direction, result.Normal, result.Lateral);
                return;
            }

            while (lo < hi - 1) {
                int mid = (lo + hi) / 2;
                if (points[mid].SpineArc <= arc) lo = mid;
                else hi = mid;
            }

            Point a = points[lo];
            Point b = points[lo + 1];

            float segStart = a.SpineArc;
            float segEnd = b.SpineArc;
            float segLen = segEnd - segStart;
            float t = segLen > 0f ? (arc - segStart) / segLen : 0f;

            float3 posA = a.SpinePosition(a.HeartOffset);
            float3 posB = b.SpinePosition(b.HeartOffset);

            result = new SplinePoint(
                arc,
                math.lerp(posA, posB, t),
                math.normalize(math.lerp(a.Direction, b.Direction, t)),
                math.normalize(math.lerp(a.Normal, b.Normal, t)),
                math.normalize(math.lerp(a.Lateral, b.Lateral, t))
            );
        }
    }
}
