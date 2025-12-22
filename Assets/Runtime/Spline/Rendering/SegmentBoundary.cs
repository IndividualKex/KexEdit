using Unity.Burst;

namespace KexEdit.Spline.Rendering {
    [BurstCompile]
    public readonly struct SegmentBoundary {
        public readonly float StartArc;
        public readonly float EndArc;
        public readonly float Scale;
        public readonly int SplineStartIndex;
        public readonly int PieceIndex;

        public SegmentBoundary(float startArc, float endArc, float scale, int splineStartIndex, int pieceIndex = 0) {
            StartArc = startArc;
            EndArc = endArc;
            Scale = scale;
            SplineStartIndex = splineStartIndex;
            PieceIndex = pieceIndex;
        }

        public float Length => EndArc - StartArc;
    }
}
