using System;
using KexEdit.Rendering;
using KexEdit.Spline.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    public class PieceStyleSingleton : IComponentData, IDisposable {
        public PieceMesh[] AllPieces;
        public NativeArray<TrackPiece> TrackPieces;
        public NativeArray<StylePieceRange> StyleRanges;

        public void Dispose() {
            if (TrackPieces.IsCreated) TrackPieces.Dispose();
            if (StyleRanges.IsCreated) StyleRanges.Dispose();
        }
    }
}
