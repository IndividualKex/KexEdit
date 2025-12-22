using KexEdit.Spline;
using KexEdit.Spline.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.Legacy.Debug {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SegmentationGizmos : SystemBase {
        private const float NominalSegmentLength = 10f;

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

        protected override void OnCreate() {
            RequireForUpdate<TrackSingleton>();
        }

        protected override void OnUpdate() {
            var singleton = SystemAPI.GetSingleton<TrackSingleton>();
            if (!singleton.Value.IsCreated) return;
            ref readonly var track = ref singleton.Value;

            var segments = new NativeList<SegmentBoundary>(16, Allocator.Temp);
            int colorIndex = 0;

            for (int s = 0; s < track.SectionCount; s++) {
                var section = track.Sections[s];
                if (!section.HasSpline) continue;

                var sectionSpline = track.SplinePoints.AsArray()
                    .GetSubArray(section.SplineStartIndex, section.SplineLength);

                segments.Clear();
                SegmentationMath.ComputeSegments(section.ArcStart, section.ArcEnd, NominalSegmentLength, ref segments);

                for (int i = 0; i < segments.Length; i++) {
                    var boundary = segments[i];
                    var color = SegmentColors[colorIndex % SegmentColors.Length];
                    colorIndex++;

                    DrawSegment(sectionSpline, boundary.StartArc, boundary.EndArc, color);
                }
            }

            segments.Dispose();
        }

        private static void DrawSegment(
            NativeArray<SplinePoint> spline,
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
