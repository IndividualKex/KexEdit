using KexEdit.Sim.Schema;
using KexEdit.Spline.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Rendering {
    [BurstCompile]
    public static class StyleBreakpointDetector {
        [BurstCompile]
        public static void DetectAllBreakpoints(
            in Track.Track track,
            in KeyframeStore keyframes,
            int maxStyleIndex,
            ref NativeList<StyleBreakpoint> allBreakpoints) {

            var sectionToNode = new NativeHashMap<int, uint>(track.SectionCount, Allocator.Temp);
            var nodeKeys = track.NodeToSection.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < nodeKeys.Length; i++) {
                uint nodeId = nodeKeys[i];
                if (track.NodeToSection.TryGetValue(nodeId, out int sectionIdx)) {
                    sectionToNode.TryAdd(sectionIdx, nodeId);
                }
            }
            nodeKeys.Dispose();

            for (int s = 0; s < track.SectionCount; s++) {
                var section = track.Sections[s];
                if (!section.IsValid || !section.Rendered || !section.HasSpline) continue;

                float sectionArc = section.ArcEnd - section.ArcStart;
                if (sectionArc <= 0f) continue;

                uint nodeId = sectionToNode.TryGetValue(s, out uint nid) ? nid : 0;
                DetectBreakpoints(in section, in keyframes, nodeId, section.StyleIndex, maxStyleIndex, s, ref allBreakpoints);
            }

            sectionToNode.Dispose();
        }

        [BurstCompile]
        public static void DetectBreakpoints(
            in Track.Section section,
            in KeyframeStore keyframes,
            uint nodeId,
            int defaultStyleIndex,
            int maxStyleIndex,
            int sectionIndex,
            ref NativeList<StyleBreakpoint> allBreakpoints) {

            float sectionArc = section.ArcEnd - section.ArcStart;

            if (nodeId == 0 || !keyframes.TryGet(nodeId, PropertyId.TrackStyle, out var styleKeyframes) || styleKeyframes.Length == 0) {
                var breakpoint = new StyleBreakpoint(sectionIndex, 0f, sectionArc, math.clamp(defaultStyleIndex, 0, maxStyleIndex));
                allBreakpoints.Add(breakpoint);
                return;
            }

            float prevArc = 0f;
            int prevStyle = math.clamp(defaultStyleIndex, 0, maxStyleIndex);

            for (int i = 0; i < styleKeyframes.Length; i++) {
                var kf = styleKeyframes[i];
                float arc = math.clamp(kf.Time, 0f, sectionArc);
                int styleValue = (int)math.round(kf.Value);
                int clampedStyle = math.clamp(styleValue, 0, maxStyleIndex);

                if (arc > prevArc) {
                    var breakpoint = new StyleBreakpoint(sectionIndex, prevArc, arc, prevStyle);
                    allBreakpoints.Add(breakpoint);
                }

                prevArc = arc;
                prevStyle = clampedStyle;
            }

            if (prevArc < sectionArc) {
                var breakpoint = new StyleBreakpoint(sectionIndex, prevArc, sectionArc, prevStyle);
                allBreakpoints.Add(breakpoint);
            }
        }
    }
}
