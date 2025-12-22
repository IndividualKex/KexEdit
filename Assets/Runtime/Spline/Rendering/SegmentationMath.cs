using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Spline.Rendering {
    [BurstCompile]
    public static class SegmentationMath {
        public const float DefaultTolerance = 0.5f;

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

        [BurstCompile]
        public static void ComputeSegments(
            float startArc,
            float endArc,
            in NativeArray<TrackPiece> pieces,
            float tolerance,
            ref NativeList<SegmentBoundary> output
        ) {
            output.Clear();

            float totalArc = endArc - startArc;
            if (totalArc <= 0f || pieces.Length == 0) return;

            float minScale = 1f - tolerance;
            float maxScale = 1f + tolerance;

            int bestPieceIdx = pieces.Length - 1;
            int bestCount = 0;
            float bestScale = 0f;

            for (int p = 0; p < pieces.Length; p++) {
                float nominalLength = pieces[p].NominalLength;
                if (nominalLength <= 0f) continue;

                int count = math.max(1, (int)math.round(totalArc / nominalLength));
                float actualLength = totalArc / count;
                float scale = actualLength / nominalLength;

                if (scale >= minScale && scale <= maxScale) {
                    bestPieceIdx = p;
                    bestCount = count;
                    bestScale = scale;
                    break;
                }

                if (p == pieces.Length - 1) {
                    bestCount = count;
                    bestScale = scale;
                }
            }

            if (bestCount == 0) {
                float nominalLength = pieces[bestPieceIdx].NominalLength;
                bestCount = math.max(1, (int)math.round(totalArc / nominalLength));
                bestScale = (totalArc / bestCount) / nominalLength;
            }

            float segmentLength = totalArc / bestCount;
            int meshIndex = pieces[bestPieceIdx].MeshIndex;

            for (int i = 0; i < bestCount; i++) {
                float segStart = startArc + i * segmentLength;
                float segEnd = startArc + (i + 1) * segmentLength;
                output.Add(new SegmentBoundary(segStart, segEnd, bestScale, 0, meshIndex));
            }
        }

        [BurstCompile]
        public static void ComputeSegments(
            float startArc,
            float endArc,
            in NativeSlice<TrackPiece> pieces,
            float tolerance,
            ref NativeList<SegmentBoundary> output
        ) {
            output.Clear();

            float totalArc = endArc - startArc;
            if (totalArc <= 0f || pieces.Length == 0) return;

            float minScale = 1f - tolerance;
            float maxScale = 1f + tolerance;

            int bestPieceIdx = pieces.Length - 1;
            int bestCount = 0;
            float bestScale = 0f;

            for (int p = 0; p < pieces.Length; p++) {
                float nominalLength = pieces[p].NominalLength;
                if (nominalLength <= 0f) continue;

                int count = math.max(1, (int)math.round(totalArc / nominalLength));
                float actualLength = totalArc / count;
                float scale = actualLength / nominalLength;

                if (scale >= minScale && scale <= maxScale) {
                    bestPieceIdx = p;
                    bestCount = count;
                    bestScale = scale;
                    break;
                }

                if (p == pieces.Length - 1) {
                    bestCount = count;
                    bestScale = scale;
                }
            }

            if (bestCount == 0) {
                float nominalLength = pieces[bestPieceIdx].NominalLength;
                bestCount = math.max(1, (int)math.round(totalArc / nominalLength));
                bestScale = (totalArc / bestCount) / nominalLength;
            }

            float segmentLength = totalArc / bestCount;
            int meshIndex = pieces[bestPieceIdx].MeshIndex;

            for (int i = 0; i < bestCount; i++) {
                float segStart = startArc + i * segmentLength;
                float segEnd = startArc + (i + 1) * segmentLength;
                output.Add(new SegmentBoundary(segStart, segEnd, bestScale, 0, meshIndex));
            }
        }
    }
}
