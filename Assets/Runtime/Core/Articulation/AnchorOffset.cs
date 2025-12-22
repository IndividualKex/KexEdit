using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Core.Articulation {
    [BurstCompile]
    public readonly struct AnchorOffset {
        public readonly float Arc;
        public readonly float3 Local;

        public AnchorOffset(float arc) {
            Arc = arc;
            Local = float3.zero;
        }

        public AnchorOffset(float arc, float3 local) {
            Arc = arc;
            Local = local;
        }

        public static AnchorOffset Zero => new(0f, float3.zero);
    }
}
