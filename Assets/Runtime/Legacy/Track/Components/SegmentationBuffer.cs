using KexEdit.Spline.Rendering;
using Unity.Entities;

namespace KexEdit.Legacy {
    public struct SegmentationBuffer : IBufferElementData {
        public SegmentBoundary Boundary;
    }
}
