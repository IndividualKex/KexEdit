using KexEdit.Spline;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.Legacy.Debug {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SegmentationGizmos : SystemBase {
        private static readonly Color[] SegmentColors = {
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.cyan,
            Color.magenta,
            new Color(1f, 0.5f, 0f),
            new Color(0.5f, 0f, 1f),
        };

        protected override void OnUpdate() {
            foreach (var (splineBuffer, segmentationBuffer) in
                SystemAPI.Query<DynamicBuffer<SplineBuffer>, DynamicBuffer<SegmentationBuffer>>()) {

                if (splineBuffer.Length < 2 || segmentationBuffer.Length == 0) continue;

                var splineArray = splineBuffer.Reinterpret<SplinePoint>().AsNativeArray();

                for (int i = 0; i < segmentationBuffer.Length; i++) {
                    var boundary = segmentationBuffer[i].Boundary;
                    var color = SegmentColors[i % SegmentColors.Length];

                    DrawSegment(splineArray, boundary.StartArc, boundary.EndArc, color);
                }
            }
        }

        private static void DrawSegment(
            Unity.Collections.NativeArray<SplinePoint> spline,
            float startArc,
            float endArc,
            Color color
        ) {
            const int steps = 20;
            float arcStep = (endArc - startArc) / steps;

            SplineInterpolation.Interpolate(spline, startArc, out var prev);

            for (int i = 1; i <= steps; i++) {
                float arc = startArc + i * arcStep;
                SplineInterpolation.Interpolate(spline, arc, out var curr);

                UnityEngine.Debug.DrawLine(prev.Position, curr.Position, color, duration: 0.1f);

                prev = curr;
            }
        }
    }
}
