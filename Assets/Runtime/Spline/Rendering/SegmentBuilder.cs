using Unity.Burst;
using Unity.Collections;

namespace KexEdit.Spline.Rendering {
    public static class SegmentBuilder {
        [BurstCompile]
        public static void Build(
            in Track.Track track,
            in NativeArray<float> selectedVisualizationData,
            in StylePieceConfig pieceConfig,
            in NativeList<StyleBreakpoint> allBreakpoints,
            ref NativeList<GPUSplinePoint> gpuSplinePoints,
            ref NativeList<float> gpuVisualizationData,
            ref NativeList<GPUSegmentBoundary> allSegments,
            ref NativeArray<int> pieceCounts) {

            var segmentBoundaries = new NativeList<SegmentBoundary>(64, Allocator.Temp);
            var sectionGPUStarts = new NativeHashMap<int, int>(track.SectionCount, Allocator.Temp);

            for (int s = 0; s < track.SectionCount; s++) {
                var section = track.Sections[s];
                if (!section.IsValid || !section.Rendered || !section.HasSpline) continue;

                float sectionArc = section.ArcEnd - section.ArcStart;
                if (sectionArc <= 0f) continue;

                int gpuSplineStart = gpuSplinePoints.Length;
                sectionGPUStarts[s] = gpuSplineStart;

                var sectionSpline = track.SplinePoints.AsArray()
                    .GetSubArray(section.SplineStartIndex, section.SplineLength);

                gpuSplinePoints.AddRange(sectionSpline.Reinterpret<GPUSplinePoint>());

                if (selectedVisualizationData.IsCreated && selectedVisualizationData.Length > 0) {
                    var sectionVisData = selectedVisualizationData
                        .GetSubArray(section.SplineStartIndex, section.SplineLength);
                    gpuVisualizationData.AddRange(sectionVisData);
                }
            }

            for (int bp = 0; bp < allBreakpoints.Length; bp++) {
                var breakpoint = allBreakpoints[bp];
                int s = breakpoint.SectionIndex;

                if (!sectionGPUStarts.TryGetValue(s, out int gpuSplineStart)) continue;

                var section = track.Sections[s];
                float sectionArc = section.ArcEnd - section.ArcStart;
                int splineCount = section.SplineLength;

                var stylePieces = pieceConfig.GetPiecesForStyle(breakpoint.StyleIndex);
                if (stylePieces.Length == 0) continue;

                segmentBoundaries.Clear();
                SegmentationMath.ComputeSegments(
                    breakpoint.StartArc, breakpoint.EndArc,
                    stylePieces, SegmentationMath.DefaultTolerance,
                    ref segmentBoundaries);

                AppendGPUSegments(
                    in segmentBoundaries,
                    section.ArcStart, sectionArc,
                    gpuSplineStart, splineCount,
                    breakpoint.SectionIndex,
                    ref allSegments, ref pieceCounts);
            }

            segmentBoundaries.Dispose();
            sectionGPUStarts.Dispose();
            SortByPiece(ref allSegments, pieceConfig.AllPieces.Length);
        }

        [BurstCompile]
        private static void AppendGPUSegments(
            in NativeList<SegmentBoundary> segmentBoundaries,
            float arcStart, float arcLength,
            int splineStartIndex, int splineCount,
            int sectionIndex,
            ref NativeList<GPUSegmentBoundary> allSegments,
            ref NativeArray<int> pieceCounts) {

            for (int i = 0; i < segmentBoundaries.Length; i++) {
                var seg = segmentBoundaries[i];
                allSegments.Add(new GPUSegmentBoundary {
                    StartArc = arcStart + seg.StartArc,
                    Length = seg.Length,
                    Scale = seg.Scale,
                    ArcStart = arcStart,
                    ArcLength = arcLength,
                    SplineStartIndex = splineStartIndex,
                    SplineCount = splineCount,
                    PieceIndex = seg.PieceIndex,
                    SectionIndex = sectionIndex
                });
                pieceCounts[seg.PieceIndex]++;
            }
        }

        [BurstCompile]
        private static void SortByPiece(ref NativeList<GPUSegmentBoundary> segments, int pieceCount) {
            if (segments.Length <= 1) return;

            var sorted = new NativeArray<GPUSegmentBoundary>(segments.Length, Allocator.Temp);
            var offsets = new NativeArray<int>(pieceCount, Allocator.Temp);
            var counts = new NativeArray<int>(pieceCount, Allocator.Temp);

            for (int i = 0; i < segments.Length; i++) {
                counts[segments[i].PieceIndex]++;
            }

            int offset = 0;
            for (int p = 0; p < pieceCount; p++) {
                offsets[p] = offset;
                offset += counts[p];
                counts[p] = 0;
            }

            for (int i = 0; i < segments.Length; i++) {
                int pieceIdx = segments[i].PieceIndex;
                int destIdx = offsets[pieceIdx] + counts[pieceIdx];
                sorted[destIdx] = segments[i];
                counts[pieceIdx]++;
            }

            segments.CopyFrom(sorted);
            sorted.Dispose();
            offsets.Dispose();
            counts.Dispose();
        }
    }
}
