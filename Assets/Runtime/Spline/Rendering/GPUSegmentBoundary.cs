using System.Runtime.InteropServices;

namespace KexEdit.Spline.Rendering {
    [StructLayout(LayoutKind.Sequential)]
    public struct GPUSegmentBoundary {
        public float StartArc;      // Segment start in section arc space
        public float Length;        // Segment length
        public float Scale;         // Mesh scale factor
        public float ArcStart;      // Section arc start (for normalization)
        public float ArcLength;     // Section total arc length
        public int SplineStartIndex;
        public int SplineCount;
        public int PieceIndex;      // Which track piece mesh to use
        public int SectionIndex;    // Track section index (for highlight lookup)
        private int _pad0;          // Padding for 16-byte alignment
        private int _pad1;
        private int _pad2;

        public const int Stride = 5 * sizeof(float) + 7 * sizeof(int); // 48 bytes (16-byte aligned)
    }
}
