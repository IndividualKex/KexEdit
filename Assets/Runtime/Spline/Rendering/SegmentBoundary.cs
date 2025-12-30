using Unity.Burst;

namespace KexEdit.Spline.Rendering {
    [BurstCompile]
    public readonly struct SegmentBoundary {
        public readonly float StartArc;
        public readonly float EndArc;
        public readonly float Scale;
        public readonly int SplineStartIndex;

        public SegmentBoundary(float startArc, float endArc, float scale, int splineStartIndex) {
            StartArc = startArc;
            EndArc = endArc;
            Scale = scale;
            SplineStartIndex = splineStartIndex;
        }

        public float Length => EndArc - StartArc;
    }
}
