using System;
using Unity.Burst;
using Unity.Collections;

namespace KexEdit.Spline.Rendering {
    public struct StylePieceRange {
        public int StartIndex;
        public int Count;

        public StylePieceRange(int startIndex, int count) {
            StartIndex = startIndex;
            Count = count;
        }
    }

    [BurstCompile]
    public struct StylePieceConfig : IDisposable {
        public NativeArray<TrackPiece> AllPieces;
        public NativeArray<StylePieceRange> StyleRanges;
        public int DefaultStyleIndex;

        public int StyleCount => StyleRanges.IsCreated ? StyleRanges.Length : 0;
        public bool IsCreated => AllPieces.IsCreated && StyleRanges.IsCreated;

        public StylePieceConfig(
            NativeArray<TrackPiece> allPieces,
            NativeArray<StylePieceRange> styleRanges,
            int defaultStyleIndex
        ) {
            AllPieces = allPieces;
            StyleRanges = styleRanges;
            DefaultStyleIndex = defaultStyleIndex;
        }

        [BurstCompile]
        public NativeSlice<TrackPiece> GetPiecesForStyle(int styleIndex) {
            if (!StyleRanges.IsCreated || styleIndex < 0 || styleIndex >= StyleRanges.Length) {
                return default;
            }

            var range = StyleRanges[styleIndex];
            return AllPieces.Slice(range.StartIndex, range.Count);
        }

        public void Dispose() {
            if (AllPieces.IsCreated) AllPieces.Dispose();
            if (StyleRanges.IsCreated) StyleRanges.Dispose();
        }
    }
}
