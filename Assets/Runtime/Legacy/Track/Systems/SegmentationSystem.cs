using KexEdit.Spline;
using KexEdit.Spline.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CoasterSyncSystem))]
    [BurstCompile]
    public partial struct SegmentationSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            foreach (var (splineBuffer, segmentationBuffer, segParams) in
                SystemAPI.Query<DynamicBuffer<SplineBuffer>, DynamicBuffer<SegmentationBuffer>, SegmentationParams>()) {

                if (splineBuffer.Length < 2) {
                    segmentationBuffer.Clear();
                    continue;
                }

                float startArc = splineBuffer[0].Point.Arc;
                float endArc = splineBuffer[^1].Point.Arc;

                var tempSegments = new NativeList<SegmentBoundary>(16, Allocator.Temp);
                SegmentationMath.ComputeSegments(startArc, endArc, segParams.NominalLength, ref tempSegments);

                var splineArray = splineBuffer.Reinterpret<SplinePoint>().AsNativeArray();

                segmentationBuffer.Clear();
                for (int i = 0; i < tempSegments.Length; i++) {
                    var seg = tempSegments[i];
                    int startIdx = SplineInterpolation.FindIndex(splineArray, seg.StartArc);
                    segmentationBuffer.Add(new SegmentationBuffer {
                        Boundary = new SegmentBoundary(seg.StartArc, seg.EndArc, seg.Scale, startIdx)
                    });
                }
            }
        }
    }
}
