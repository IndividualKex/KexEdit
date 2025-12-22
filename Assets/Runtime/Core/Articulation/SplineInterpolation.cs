using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Core.Articulation {
    [BurstCompile]
    public static class SplineInterpolation {
        [BurstCompile]
        public static int FindIndex(in NativeArray<SplinePoint> spline, float arc) {
            if (spline.Length < 2) return 0;

            float startArc = spline[0].Arc;
            float endArc = spline[^1].Arc;

            if (arc <= startArc) return 0;
            if (arc >= endArc) return spline.Length - 2;

            int lo = 0;
            int hi = spline.Length - 1;
            while (lo < hi - 1) {
                int mid = (lo + hi) / 2;
                if (spline[mid].Arc <= arc) lo = mid;
                else hi = mid;
            }

            return lo;
        }

        [BurstCompile]
        public static float GetInterpolationFactor(in NativeArray<SplinePoint> spline, int index, float arc) {
            if (index < 0 || index >= spline.Length - 1) return 0f;

            float segmentStart = spline[index].Arc;
            float segmentEnd = spline[index + 1].Arc;
            float segmentLength = segmentEnd - segmentStart;

            if (segmentLength <= 0f) return 0f;

            return math.saturate((arc - segmentStart) / segmentLength);
        }

        [BurstCompile]
        public static void Interpolate(in NativeArray<SplinePoint> spline, float arc, out SplinePoint result) {
            if (spline.Length == 0) {
                result = SplinePoint.Default;
                return;
            }
            if (spline.Length == 1) {
                result = spline[0];
                return;
            }

            int index = FindIndex(spline, arc);
            float t = GetInterpolationFactor(spline, index, arc);

            SplinePoint.Lerp(spline[index], spline[index + 1], t, out result);
        }

        public static float StartArc(in NativeArray<SplinePoint> spline) =>
            spline.Length > 0 ? spline[0].Arc : 0f;

        public static float EndArc(in NativeArray<SplinePoint> spline) =>
            spline.Length > 0 ? spline[^1].Arc : 0f;
    }
}
