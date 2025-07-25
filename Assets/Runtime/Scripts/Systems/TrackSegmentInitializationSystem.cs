using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TrackSegmentInitializationSystem : SystemBase {
        private EntityQuery _segmentQuery;

        protected override void OnCreate() {
            _segmentQuery = EntityManager.CreateEntityQuery(typeof(SectionReference));
            RequireForUpdate<TrackStyleSettings>();
        }

        protected override void OnUpdate() {
            var settings = SystemAPI.ManagedAPI.GetSingleton<TrackStyleSettings>();
            if (settings.Styles.Count == 0) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            int count = _segmentQuery.CalculateEntityCount();
            using var existing = new NativeParallelHashSet<Entity>(count, Allocator.Temp);
            foreach (var (section, segment) in SystemAPI.Query<SectionReference, Segment>()) {
                var sectionStyleHash = SystemAPI.GetComponent<StyleHash>(section);
                if (segment.StyleHash == sectionStyleHash.Value) {
                    existing.Add(section);
                }
            }

            foreach (var (render, trackStyleBuffer, entity) in SystemAPI
                .Query<Render, DynamicBuffer<TrackStyleKeyframe>>()
                .WithAll<Point>()
                .WithEntityAccess()
            ) {
                if (!render || existing.Contains(entity)) continue;

                var points = SystemAPI.GetBuffer<Point>(entity);
                if (points.Length == 0) continue;

                uint styleHash = SystemAPI.GetComponent<StyleHash>(entity);
                var overrides = SystemAPI.GetComponent<PropertyOverrides>(entity);

                var breakpoints = overrides.TrackStyle
                    ? DetectManualStyleBreakpoints(trackStyleBuffer, points, settings)
                    : DetectAutoStyleBreakpoints(points, settings);

                for (int i = 0; i < breakpoints.Length; i++) {
                    var breakpoint = breakpoints[i];
                    int styleIndex = breakpoint.StyleIndex;
                    var selectedStyle = settings.Styles[styleIndex];

                    var segmentEntity = ecb.CreateEntity();
                    ecb.AddComponent<SectionReference>(segmentEntity, entity);
                    ecb.AddComponent<SelectedBlend>(segmentEntity);
                    ecb.AddComponent<TrackHash>(segmentEntity);
                    ecb.AddComponent<Render>(segmentEntity, render);
                    ecb.AddComponent(segmentEntity, new Segment {
                        StartTime = breakpoint.StartTime,
                        EndTime = breakpoint.EndTime,
                        StyleHash = styleHash
                    });
                    ecb.AddComponent(segmentEntity, new TrackStyle {
                        DuplicationMeshes = new(selectedStyle.DuplicationMeshes),
                        ExtrusionMeshes = new(selectedStyle.ExtrusionMeshes),
                        StartCapMeshes = new(selectedStyle.StartCapMeshes),
                        EndCapMeshes = new(selectedStyle.EndCapMeshes),
                        Spacing = selectedStyle.Spacing,
                        Threshold = selectedStyle.Threshold,
                        Version = settings.Version
                    });
                    ecb.AddBuffer<TrackPoint>(segmentEntity);
                    ecb.SetName(segmentEntity, $"Segment {i}");
                }

                breakpoints.Dispose();
            }

            ecb.Playback(EntityManager);
        }

        private NativeArray<StyleBreakpoint> DetectManualStyleBreakpoints(
            DynamicBuffer<TrackStyleKeyframe> keyframes,
            DynamicBuffer<Point> points,
            TrackStyleSettings settings
        ) {
            var breakpoints = new NativeList<StyleBreakpoint>(Allocator.TempJob);

            new ManualBreakpointJob {
                Keyframes = keyframes,
                Points = points,
                MaxStyleCount = settings.Styles.Count,
                Breakpoints = breakpoints
            }.Run();

            var result = breakpoints.ToArray(Allocator.Temp);

            breakpoints.Dispose();

            return result;
        }

        private NativeArray<StyleBreakpoint> DetectAutoStyleBreakpoints(
            DynamicBuffer<Point> points,
            TrackStyleSettings settings
        ) {
            if (!settings.AutoStyle) {
                return DetectDefaultStyleBreakpoints(points, settings);
            }

            var breakpoints = new NativeList<StyleBreakpoint>(Allocator.TempJob);
            var thresholds = new NativeArray<float>(settings.Styles.Count, Allocator.TempJob);

            for (int i = 0; i < settings.Styles.Count; i++) {
                thresholds[i] = settings.Styles[i].Threshold;
            }

            new StressBreakpointJob {
                Points = points,
                StyleThresholds = thresholds,
                Breakpoints = breakpoints
            }.Run();

            var result = breakpoints.ToArray(Allocator.Temp);

            breakpoints.Dispose();
            thresholds.Dispose();

            return result;
        }

        private NativeArray<StyleBreakpoint> DetectDefaultStyleBreakpoints(
            DynamicBuffer<Point> points,
            TrackStyleSettings settings
        ) {
            int defaultStyleIndex = math.clamp(settings.DefaultStyle, 0, settings.Styles.Count - 1);
            var breakpoints = new NativeList<StyleBreakpoint>(Allocator.TempJob);

            new DefaultStyleBreakpointJob {
                Points = points,
                DefaultStyleIndex = defaultStyleIndex,
                Breakpoints = breakpoints
            }.Run();

            var result = breakpoints.ToArray(Allocator.Temp);
            breakpoints.Dispose();
            return result;
        }

        private struct StyleBreakpoint {
            public float StartTime;
            public float EndTime;
            public int StyleIndex;
        }

        [BurstCompile]
        private struct ManualBreakpointJob : IJob {
            public NativeList<StyleBreakpoint> Breakpoints;

            [ReadOnly]
            public DynamicBuffer<TrackStyleKeyframe> Keyframes;

            [ReadOnly]
            public DynamicBuffer<Point> Points;

            [ReadOnly]
            public int MaxStyleCount;

            public void Execute() {
                if (Keyframes.Length == 0) {
                    Breakpoints.Add(new StyleBreakpoint {
                        StartTime = 0f,
                        EndTime = float.MaxValue,
                        StyleIndex = 0
                    });
                }
                else {
                    GenerateKeyframeBreakpoints();
                }

                PostProcessMaxLength();
            }

            private void GenerateKeyframeBreakpoints() {
                int currentStyleIndex = (int)math.round(Keyframes[0].Value.Value);
                currentStyleIndex = math.clamp(currentStyleIndex, 0, MaxStyleCount - 1);
                float segmentStartTime = 0f;

                if (Keyframes[0].Value.Time > 0f) {
                    Breakpoints.Add(new StyleBreakpoint {
                        StartTime = 0f,
                        EndTime = Keyframes[0].Value.Time,
                        StyleIndex = currentStyleIndex
                    });
                    segmentStartTime = Keyframes[0].Value.Time;
                }

                for (int i = 0; i < Keyframes.Length; i++) {
                    var keyframe = Keyframes[i].Value;
                    int styleIndex = (int)math.round(keyframe.Value);
                    styleIndex = math.clamp(styleIndex, 0, MaxStyleCount - 1);

                    Breakpoints.Add(new StyleBreakpoint {
                        StartTime = segmentStartTime,
                        EndTime = keyframe.Time,
                        StyleIndex = currentStyleIndex
                    });
                    segmentStartTime = keyframe.Time;
                    currentStyleIndex = styleIndex;
                }

                Breakpoints.Add(new StyleBreakpoint {
                    StartTime = segmentStartTime,
                    EndTime = float.MaxValue,
                    StyleIndex = currentStyleIndex
                });
            }

            private void PostProcessMaxLength() {
                var processedBreakpoints = new NativeList<StyleBreakpoint>(Allocator.Temp);

                for (int i = 0; i < Breakpoints.Length; i++) {
                    var breakpoint = Breakpoints[i];
                    var segmentLength = CalculateTrackLengthBetweenTimes(breakpoint.StartTime, breakpoint.EndTime);

                    if (segmentLength <= MAX_SEGMENT_LENGTH) {
                        processedBreakpoints.Add(breakpoint);
                    }
                    else {
                        SubdivideSegment(ref processedBreakpoints, breakpoint);
                    }
                }

                Breakpoints.Clear();
                Breakpoints.AddRange(processedBreakpoints.AsArray());

                processedBreakpoints.Dispose();
            }

            private void SubdivideSegment(ref NativeList<StyleBreakpoint> processedBreakpoints, StyleBreakpoint segment) {
                if (segment.EndTime == float.MaxValue) {
                    SubdivideInfiniteSegment(ref processedBreakpoints, segment);
                }
                else {
                    SubdivideFiniteSegment(ref processedBreakpoints, segment);
                }
            }

            private void SubdivideFiniteSegment(ref NativeList<StyleBreakpoint> processedBreakpoints, StyleBreakpoint segment) {
                var segmentLength = CalculateTrackLengthBetweenTimes(segment.StartTime, segment.EndTime);
                var segmentCount = (int)math.ceil(segmentLength / MAX_SEGMENT_LENGTH);
                var segmentDuration = segment.EndTime - segment.StartTime;
                var subSegmentDuration = segmentDuration / segmentCount;

                for (int i = 0; i < segmentCount; i++) {
                    var subStartTime = segment.StartTime + i * subSegmentDuration;
                    var subEndTime = (i == segmentCount - 1) ? segment.EndTime : segment.StartTime + (i + 1) * subSegmentDuration;

                    processedBreakpoints.Add(new StyleBreakpoint {
                        StartTime = subStartTime,
                        EndTime = subEndTime,
                        StyleIndex = segment.StyleIndex
                    });
                }
            }

            private void SubdivideInfiniteSegment(ref NativeList<StyleBreakpoint> processedBreakpoints, StyleBreakpoint segment) {
                if (Points.Length == 0) {
                    processedBreakpoints.Add(segment);
                    return;
                }

                int startIndex = math.clamp((int)(segment.StartTime * HZ), 0, Points.Length - 1);
                float segmentStartTime = segment.StartTime;
                float segmentStartLength = Points[startIndex].Value.TotalLength;

                for (int i = startIndex; i < Points.Length - 1; i++) {
                    float currentLength = Points[i].Value.TotalLength;
                    float currentSegmentLength = currentLength - segmentStartLength;

                    if (currentSegmentLength >= MAX_SEGMENT_LENGTH) {
                        float pointTime = i / HZ;
                        processedBreakpoints.Add(new StyleBreakpoint {
                            StartTime = segmentStartTime,
                            EndTime = pointTime,
                            StyleIndex = segment.StyleIndex
                        });
                        segmentStartTime = pointTime;
                        segmentStartLength = currentLength;
                    }
                }

                processedBreakpoints.Add(new StyleBreakpoint {
                    StartTime = segmentStartTime,
                    EndTime = float.MaxValue,
                    StyleIndex = segment.StyleIndex
                });
            }

            private float CalculateTrackLengthBetweenTimes(float startTime, float endTime) {
                if (Points.Length == 0) return 0f;
                if (endTime == float.MaxValue) return float.MaxValue;

                int startIndex = math.clamp((int)(startTime * HZ), 0, Points.Length - 1);
                int endIndex = math.clamp((int)(endTime * HZ), 0, Points.Length - 1);

                if (startIndex >= endIndex) return 0f;

                float startLength = Points[startIndex].Value.TotalLength;
                float endLength = Points[endIndex].Value.TotalLength;

                return endLength - startLength;
            }
        }

        [BurstCompile]
        private struct StressBreakpointJob : IJob {
            public NativeList<StyleBreakpoint> Breakpoints;

            [ReadOnly]
            public DynamicBuffer<Point> Points;

            [ReadOnly]
            public NativeArray<float> StyleThresholds;

            public void Execute() {
                if (Points.Length == 0 || StyleThresholds.Length == 0) {
                    Breakpoints.Add(new StyleBreakpoint {
                        StartTime = 0f,
                        EndTime = float.MaxValue,
                        StyleIndex = 0
                    });
                    return;
                }

                var allStress = new NativeArray<float>(Points.Length, Allocator.Temp);
                for (int i = 0; i < Points.Length; i++) {
                    allStress[i] = CalculateStress(Points[i]);
                }

                int halfWindow = STRESS_ROLLING_WINDOW / 2;
                int windowSize = math.min(STRESS_ROLLING_WINDOW, Points.Length);

                float windowSum = 0f;
                for (int i = 0; i < windowSize; i++) {
                    windowSum += allStress[i];
                }

                int currentStyleIndex = SelectStyleByStress(windowSum / windowSize);
                float segmentStartTime = 0f;
                float segmentStartLength = Points[0].Value.TotalLength;

                int lastWindowStart = 0;
                int lastWindowEnd = windowSize - 1;

                for (int center = 10; center < Points.Length; center += 10) {
                    int windowStart = math.max(0, center - halfWindow);
                    int windowEnd = math.min(Points.Length - 1, center + halfWindow);

                    while (lastWindowStart < windowStart) {
                        windowSum -= allStress[lastWindowStart];
                        lastWindowStart++;
                    }
                    while (lastWindowEnd < windowEnd) {
                        lastWindowEnd++;
                        if (lastWindowEnd < allStress.Length) {
                            windowSum += allStress[lastWindowEnd];
                        }
                    }
                    while (lastWindowStart > windowStart) {
                        lastWindowStart--;
                        windowSum += allStress[lastWindowStart];
                    }
                    while (lastWindowEnd > windowEnd) {
                        windowSum -= allStress[lastWindowEnd];
                        lastWindowEnd--;
                    }

                    int actualWindowSize = lastWindowEnd - lastWindowStart + 1;
                    float avgStress = windowSum / actualWindowSize;
                    int newStyleIndex = SelectStyleByStress(avgStress);

                    float currentLength = Points[center].Value.TotalLength;
                    float segmentLength = currentLength - segmentStartLength;

                    bool styleChanged = newStyleIndex != currentStyleIndex;
                    bool exceedsMaxLength = segmentLength >= MAX_SEGMENT_LENGTH;

                    if ((styleChanged && segmentLength >= MIN_SEGMENT_LENGTH) || exceedsMaxLength) {
                        float pointTime = center / HZ;

                        Breakpoints.Add(new StyleBreakpoint {
                            StartTime = segmentStartTime,
                            EndTime = pointTime,
                            StyleIndex = currentStyleIndex
                        });
                        segmentStartTime = pointTime;
                        segmentStartLength = currentLength;

                        if (styleChanged) {
                            currentStyleIndex = newStyleIndex;
                        }
                    }
                }

                allStress.Dispose();

                var finalSegmentLength = Points[^1].Value.TotalLength - segmentStartLength;
                if (finalSegmentLength > MAX_SEGMENT_LENGTH) {
                    var segmentCount = (int)math.ceil(finalSegmentLength / MAX_SEGMENT_LENGTH);
                    var segmentLengthStep = finalSegmentLength / segmentCount;

                    for (int i = 0; i < segmentCount - 1; i++) {
                        var targetLength = segmentStartLength + (i + 1) * segmentLengthStep;
                        int pointIndex = FindPointIndexByLength(targetLength);
                        float pointTime = pointIndex / HZ;

                        Breakpoints.Add(new StyleBreakpoint {
                            StartTime = segmentStartTime,
                            EndTime = pointTime,
                            StyleIndex = currentStyleIndex
                        });
                        segmentStartTime = pointTime;
                    }
                }

                Breakpoints.Add(new StyleBreakpoint {
                    StartTime = segmentStartTime,
                    EndTime = float.MaxValue,
                    StyleIndex = currentStyleIndex
                });
            }

            private int FindPointIndexByLength(float targetLength) {
                for (int i = 0; i < Points.Length; i++) {
                    if (Points[i].Value.TotalLength >= targetLength) {
                        return i;
                    }
                }
                return Points.Length - 1;
            }

            private float CalculateStress(PointData point) {
                float pitchCurvature = math.abs(math.radians(point.PitchFromLast));
                float yawCurvature = math.abs(math.radians(point.YawFromLast));
                float totalCurvature = math.max(MIN_CURVATURE, math.sqrt(pitchCurvature * pitchCurvature + yawCurvature * yawCurvature));
                float structuralStress = totalCurvature * point.Velocity * point.Velocity;

                float torsionalStress = math.abs(point.RollSpeed) * point.Velocity * TORSION_STRESS_FACTOR;

                return structuralStress + torsionalStress;
            }

            private int SelectStyleByStress(float stress) {
                int selectedIndex = 0;
                for (int i = 0; i < StyleThresholds.Length; i++) {
                    float threshold = StyleThresholds[i];
                    if (stress >= threshold) {
                        selectedIndex = i;
                    }
                }
                return selectedIndex;
            }
        }

        [BurstCompile]
        private struct DefaultStyleBreakpointJob : IJob {
            public NativeList<StyleBreakpoint> Breakpoints;

            [ReadOnly]
            public DynamicBuffer<Point> Points;

            [ReadOnly]
            public int DefaultStyleIndex;

            public void Execute() {
                Breakpoints.Add(new StyleBreakpoint {
                    StartTime = 0f,
                    EndTime = float.MaxValue,
                    StyleIndex = DefaultStyleIndex
                });

                PostProcessMaxLength();
            }

            private void PostProcessMaxLength() {
                var processedBreakpoints = new NativeList<StyleBreakpoint>(Allocator.Temp);

                for (int i = 0; i < Breakpoints.Length; i++) {
                    var breakpoint = Breakpoints[i];
                    var segmentLength = CalculateTrackLengthBetweenTimes(breakpoint.StartTime, breakpoint.EndTime);

                    if (segmentLength <= MAX_SEGMENT_LENGTH) {
                        processedBreakpoints.Add(breakpoint);
                    }
                    else {
                        SubdivideSegment(ref processedBreakpoints, breakpoint);
                    }
                }

                Breakpoints.Clear();
                Breakpoints.AddRange(processedBreakpoints.AsArray());

                processedBreakpoints.Dispose();
            }

            private void SubdivideSegment(ref NativeList<StyleBreakpoint> processedBreakpoints, StyleBreakpoint segment) {
                if (segment.EndTime == float.MaxValue) {
                    SubdivideInfiniteSegment(ref processedBreakpoints, segment);
                }
                else {
                    SubdivideFiniteSegment(ref processedBreakpoints, segment);
                }
            }

            private void SubdivideFiniteSegment(ref NativeList<StyleBreakpoint> processedBreakpoints, StyleBreakpoint segment) {
                var segmentLength = CalculateTrackLengthBetweenTimes(segment.StartTime, segment.EndTime);
                var segmentCount = (int)math.ceil(segmentLength / MAX_SEGMENT_LENGTH);
                var segmentDuration = segment.EndTime - segment.StartTime;
                var subSegmentDuration = segmentDuration / segmentCount;

                for (int i = 0; i < segmentCount; i++) {
                    var subStartTime = segment.StartTime + i * subSegmentDuration;
                    var subEndTime = (i == segmentCount - 1) ? segment.EndTime : segment.StartTime + (i + 1) * subSegmentDuration;

                    processedBreakpoints.Add(new StyleBreakpoint {
                        StartTime = subStartTime,
                        EndTime = subEndTime,
                        StyleIndex = segment.StyleIndex
                    });
                }
            }

            private void SubdivideInfiniteSegment(ref NativeList<StyleBreakpoint> processedBreakpoints, StyleBreakpoint segment) {
                if (Points.Length == 0) {
                    processedBreakpoints.Add(segment);
                    return;
                }

                int startIndex = math.clamp((int)(segment.StartTime * HZ), 0, Points.Length - 1);
                float segmentStartTime = segment.StartTime;
                float segmentStartLength = Points[startIndex].Value.TotalLength;

                for (int i = startIndex; i < Points.Length - 1; i++) {
                    float currentLength = Points[i].Value.TotalLength;
                    float currentSegmentLength = currentLength - segmentStartLength;

                    if (currentSegmentLength >= MAX_SEGMENT_LENGTH) {
                        float pointTime = i / HZ;
                        processedBreakpoints.Add(new StyleBreakpoint {
                            StartTime = segmentStartTime,
                            EndTime = pointTime,
                            StyleIndex = segment.StyleIndex
                        });
                        segmentStartTime = pointTime;
                        segmentStartLength = currentLength;
                    }
                }

                processedBreakpoints.Add(new StyleBreakpoint {
                    StartTime = segmentStartTime,
                    EndTime = float.MaxValue,
                    StyleIndex = segment.StyleIndex
                });
            }

            private float CalculateTrackLengthBetweenTimes(float startTime, float endTime) {
                if (Points.Length == 0) return 0f;
                if (endTime == float.MaxValue) return float.MaxValue;

                int startIndex = math.clamp((int)(startTime * HZ), 0, Points.Length - 1);
                int endIndex = math.clamp((int)(endTime * HZ), 0, Points.Length - 1);

                if (startIndex >= endIndex) return 0f;

                float startLength = Points[startIndex].Value.TotalLength;
                float endLength = Points[endIndex].Value.TotalLength;

                return endLength - startLength;
            }
        }
    }
}
