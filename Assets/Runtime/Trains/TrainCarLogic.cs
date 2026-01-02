using KexEdit.Sim;
using KexEdit.Spline;
using KexEdit.Track;
using KexEdit.Trains.Sim;
using Unity.Burst;
using TrackData = KexEdit.Track.Track;

namespace KexEdit.Trains {
    [BurstCompile]
    public static class TrainCarLogic {
        private const int MaxLinkDepth = 10;

        [BurstCompile]
        public static void PositionCar(
            in TrackData track,
            int sectionIndex,
            float arc,
            float offset,
            int facing,
            out SplinePoint result
        ) {
            track.SampleSplinePoint(sectionIndex, arc + offset * facing, out result);
        }

        [BurstCompile]
        public static void PositionCarWithOverhang(
            in TrackData track,
            int sectionIndex,
            float arc,
            float offset,
            int facing,
            out SplinePoint result
        ) {
            if (sectionIndex < 0 || sectionIndex >= track.SectionCount) {
                result = SplinePoint.Default;
                return;
            }

            var section = track.Sections[sectionIndex];
            if (!section.IsValid) {
                result = SplinePoint.Default;
                return;
            }

            float targetArc = arc + offset * facing;

            if (targetArc >= section.ArcStart && targetArc <= section.ArcEnd) {
                track.SampleSplinePoint(sectionIndex, targetArc, out result);
                return;
            }

            if (targetArc < section.ArcStart) {
                float overhang = section.ArcStart - targetArc;
                if (TryFollowLink(in track, in section.Prev, overhang, out result, MaxLinkDepth)) {
                    return;
                }
                track.Extrapolate(sectionIndex, targetArc, fromEnd: false, out result);
                return;
            }

            if (targetArc > section.ArcEnd) {
                float overhang = targetArc - section.ArcEnd;
                if (TryFollowLink(in track, in section.Next, overhang, out result, MaxLinkDepth)) {
                    return;
                }
                track.Extrapolate(sectionIndex, targetArc, fromEnd: true, out result);
                return;
            }

            result = SplinePoint.Default;
        }

        [BurstCompile]
        public static bool TryGetSplinePoint(
            in SimFollower follower,
            in TrackData track,
            float offset,
            out SplinePoint result
        ) {
            result = SplinePoint.Default;

            if (!track.IsCreated || track.TraversalCount == 0) return false;
            if (follower.TraversalIndex < 0 || follower.TraversalIndex >= track.TraversalCount) return false;

            int sectionIdx = track.TraversalOrder[follower.TraversalIndex];
            var section = track.Sections[sectionIdx];
            if (!section.IsValid) return false;

            SimFollowerLogic.GetCurrentPoint(in follower, in track, out Point point);
            float arc = point.SpineArc;

            PositionCarWithOverhang(in track, sectionIdx, arc, offset, follower.Facing, out var raw);

            result = new SplinePoint(
                raw.Arc,
                raw.Position,
                raw.Direction * follower.Facing,
                raw.Normal,
                raw.Lateral * follower.Facing
            );
            return true;
        }

        [BurstCompile]
        private static bool TryFollowLink(
            in TrackData track,
            in SectionLink link,
            float overhang,
            out SplinePoint result,
            int remainingDepth
        ) {
            result = SplinePoint.Default;
            if (!link.IsValid || remainingDepth <= 0) return false;

            var target = track.Sections[link.Index];
            if (!target.IsValid) return false;

            float targetLength = target.ArcEnd - target.ArcStart;

            if (link.AtStart) {
                // Connection is at target's START, traverse forward
                if (overhang <= targetLength) {
                    float mappedArc = target.ArcStart + overhang;
                    track.SampleSplinePoint(link.Index, mappedArc, out result);
                }
                else {
                    // Overhang exceeds section - recursively follow Next
                    float remainingOverhang = overhang - targetLength;
                    if (!TryFollowLink(in track, in target.Next, remainingOverhang, out result, remainingDepth - 1)) {
                        return false;
                    }
                }
            }
            else {
                // Connection is at target's END, traverse backward
                if (overhang <= targetLength) {
                    float mappedArc = target.ArcEnd - overhang;
                    track.SampleSplinePoint(link.Index, mappedArc, out result);
                }
                else {
                    // Overhang exceeds section - recursively follow Prev
                    float remainingOverhang = overhang - targetLength;
                    if (!TryFollowLink(in track, in target.Prev, remainingOverhang, out result, remainingDepth - 1)) {
                        return false;
                    }
                }
            }

            if (link.Flip) {
                result = new SplinePoint(
                    result.Arc,
                    result.Position,
                    -result.Direction,
                    result.Normal,
                    -result.Lateral
                );
            }

            return true;
        }
    }
}
