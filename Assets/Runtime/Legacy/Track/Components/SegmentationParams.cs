using KexEdit.Spline.Rendering;
using Unity.Entities;

namespace KexEdit.Legacy {
    public struct SegmentationParams : IComponentData {
        public float NominalLength;

        public static SegmentationParams Default => new() {
            NominalLength = SegmentationMath.DefaultNominalLength
        };
    }
}
