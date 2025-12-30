using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Spline.Rendering {
    [BurstCompile]
    public static class SegmentationMath {
        public const float DefaultNominalLength = 10f;

        [BurstCompile]
        public static int ComputeSegmentCount(float totalArc, float nominalLength) {
            if (totalArc <= 0f || nominalLength <= 0f) return 0;
            return math.max(1, (int)math.round(totalArc / nominalLength));
        }

        [BurstCompile]
        public static void ComputeSegments(
            float startArc,
            float endArc,
            float nominalLength,
            ref NativeList<SegmentBoundary> output
        ) {
            output.Clear();

            float totalArc = endArc - startArc;
            int count = ComputeSegmentCount(totalArc, nominalLength);
            if (count == 0) return;

            float actualLength = totalArc / count;
            float scale = actualLength / nominalLength;

            for (int i = 0; i < count; i++) {
                float segStart = startArc + i * actualLength;
                float segEnd = startArc + (i + 1) * actualLength;
                output.Add(new SegmentBoundary(segStart, segEnd, scale, 0));
            }
        }
    }
}
