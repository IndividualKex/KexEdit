using KexEdit.Sim;
using Unity.Burst;
using Unity.Mathematics;
using TrackData = KexEdit.Track.Track;

namespace KexEdit.Trains.Sim {
    [BurstCompile]
    public static class SimFollowerLogic {
        [BurstCompile]
        public static void Advance(
            ref SimFollower follower,
            in TrackData track,
            float deltaTime,
            float hz,
            bool wrapAtEnd,
            out Point point
        ) {
            if (track.TraversalCount == 0) {
                point = Point.Default;
                return;
            }

            follower.PointIndex += deltaTime * hz;

            int maxIterations = track.TraversalCount * 2 + 10;
            int iterations = 0;

            while (iterations++ < maxIterations) {
                if (follower.TraversalIndex >= track.TraversalCount) {
                    if (wrapAtEnd) {
                        follower.TraversalIndex = 0;
                    }
                    else {
                        follower.TraversalIndex = track.TraversalCount - 1;
                        int lastSectionIdx = track.TraversalOrder[follower.TraversalIndex];
                        var lastSection = track.Sections[lastSectionIdx];
                        if (lastSection.IsValid) {
                            follower.PointIndex = lastSection.Length - 1;
                        }
                        GetCurrentPoint(in follower, in track, out point);
                        return;
                    }
                }

                int sectionIdx = track.TraversalOrder[follower.TraversalIndex];
                var section = track.Sections[sectionIdx];
                if (!section.IsValid) {
                    follower.TraversalIndex++;
                    follower.PointIndex = 0f;
                    continue;
                }

                int sectionLength = section.Length;
                if (follower.PointIndex >= sectionLength - 1) {
                    float overshoot = follower.PointIndex - (sectionLength - 1);
                    follower.TraversalIndex++;
                    follower.PointIndex = overshoot;
                    continue;
                }

                break;
            }

            if (iterations >= maxIterations) {
                follower.TraversalIndex = math.clamp(follower.TraversalIndex, 0, track.TraversalCount - 1);
                int sectionIdx = track.TraversalOrder[follower.TraversalIndex];
                var section = track.Sections[sectionIdx];
                if (section.IsValid) {
                    follower.PointIndex = section.Length - 1;
                }
                else {
                    follower.PointIndex = 0f;
                }
            }

            UpdateFacing(ref follower, in track);
            GetCurrentPoint(in follower, in track, out point);
        }

        [BurstCompile]
        private static void UpdateFacing(ref SimFollower follower, in TrackData track) {
            if (track.TraversalCount == 0) return;

            int traversalIdx = math.clamp(follower.TraversalIndex, 0, track.TraversalCount - 1);
            int sectionIdx = track.TraversalOrder[traversalIdx];
            var section = track.Sections[sectionIdx];
            follower.Facing = section.Facing;
        }

        [BurstCompile]
        public static void GetCurrentPoint(in SimFollower follower, in TrackData track, out Point point) {
            if (track.TraversalCount == 0) {
                point = Point.Default;
                return;
            }

            int traversalIdx = math.clamp(follower.TraversalIndex, 0, track.TraversalCount - 1);
            int sectionIdx = track.TraversalOrder[traversalIdx];
            var section = track.Sections[sectionIdx];

            if (!section.IsValid) {
                point = Point.Default;
                return;
            }

            int sectionLength = section.Length;
            if (sectionLength == 1) {
                point = track.Points[section.StartIndex];
                return;
            }

            int i0 = (int)math.floor(follower.PointIndex);
            float t = follower.PointIndex - i0;
            i0 = math.clamp(i0, 0, sectionLength - 2);

            int globalIndex = section.StartIndex + i0;
            Interpolate(in track.Points.ElementAt(globalIndex), in track.Points.ElementAt(globalIndex + 1), t, out point);
        }

        [BurstCompile]
        private static void Interpolate(in Point p0, in Point p1, float t, out Point result) {
            result = new Point(
                heartPosition: math.lerp(p0.HeartPosition, p1.HeartPosition, t),
                direction: math.normalizesafe(math.lerp(p0.Direction, p1.Direction, t)),
                normal: math.normalizesafe(math.lerp(p0.Normal, p1.Normal, t)),
                lateral: math.normalizesafe(math.lerp(p0.Lateral, p1.Lateral, t)),
                velocity: math.lerp(p0.Velocity, p1.Velocity, t),
                normalForce: math.lerp(p0.NormalForce, p1.NormalForce, t),
                lateralForce: math.lerp(p0.LateralForce, p1.LateralForce, t),
                heartArc: math.lerp(p0.HeartArc, p1.HeartArc, t),
                spineArc: math.lerp(p0.SpineArc, p1.SpineArc, t),
                heartAdvance: math.lerp(p0.HeartAdvance, p1.HeartAdvance, t),
                frictionOrigin: math.lerp(p0.FrictionOrigin, p1.FrictionOrigin, t),
                rollSpeed: math.lerp(p0.RollSpeed, p1.RollSpeed, t),
                heartOffset: math.lerp(p0.HeartOffset, p1.HeartOffset, t),
                friction: math.lerp(p0.Friction, p1.Friction, t),
                resistance: math.lerp(p0.Resistance, p1.Resistance, t)
            );
        }

        [BurstCompile]
        public static void SetFromProgress(ref SimFollower follower, in TrackData track, float progress) {
            if (track.TraversalCount == 0) {
                follower = SimFollower.Default;
                return;
            }

            float totalLength = 0f;
            for (int i = 0; i < track.TraversalCount; i++) {
                int sectionIdx = track.TraversalOrder[i];
                var section = track.Sections[sectionIdx];
                if (section.IsValid) {
                    totalLength += section.Length - 1;
                }
            }

            float targetDistance = math.clamp(progress, 0f, 1f) * totalLength;
            float accumulated = 0f;

            for (int i = 0; i < track.TraversalCount; i++) {
                int sectionIdx = track.TraversalOrder[i];
                var section = track.Sections[sectionIdx];
                if (!section.IsValid) continue;

                float sectionLength = section.Length - 1;
                if (accumulated + sectionLength >= targetDistance) {
                    follower.TraversalIndex = i;
                    follower.PointIndex = targetDistance - accumulated;
                    UpdateFacing(ref follower, in track);
                    return;
                }
                accumulated += sectionLength;
            }

            follower.TraversalIndex = track.TraversalCount - 1;
            int lastSectionIdx = track.TraversalOrder[follower.TraversalIndex];
            var lastSection = track.Sections[lastSectionIdx];
            follower.PointIndex = lastSection.IsValid ? lastSection.Length - 1 : 0f;
            UpdateFacing(ref follower, in track);
        }

        [BurstCompile]
        public static float GetProgress(in SimFollower follower, in TrackData track) {
            if (track.TraversalCount == 0) return 0f;

            float totalLength = 0f;
            float currentDistance = 0f;

            for (int i = 0; i < track.TraversalCount; i++) {
                int sectionIdx = track.TraversalOrder[i];
                var section = track.Sections[sectionIdx];
                if (!section.IsValid) continue;

                float sectionLength = section.Length - 1;
                if (i < follower.TraversalIndex) {
                    currentDistance += sectionLength;
                }
                else if (i == follower.TraversalIndex) {
                    currentDistance += follower.PointIndex;
                }
                totalLength += sectionLength;
            }

            return totalLength > 0f ? currentDistance / totalLength : 0f;
        }
    }
}
