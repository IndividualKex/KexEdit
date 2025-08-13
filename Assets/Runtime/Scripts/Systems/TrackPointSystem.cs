using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct TrackPointSystem : ISystem {
        private BufferLookup<Point> _pointLookup;
        private BufferLookup<TrackPoint> _trackPointLookup;

        private EntityQuery _query;
        private EntityQuery _countQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);
            _trackPointLookup = SystemAPI.GetBufferLookup<TrackPoint>(false);

            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SectionReference, Segment, TrackPoint, TrackHash>()
                .Build(state.EntityManager);

            _countQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SectionReference, Segment>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_query);
            state.RequireForUpdate<Preferences>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            _pointLookup.Update(ref state);
            _trackPointLookup.Update(ref state);

            var preferences = SystemAPI.GetSingleton<Preferences>();

            int count = _countQuery.CalculateEntityCount();
            var countMap = new NativeParallelHashMap<Entity, int>(count, Allocator.TempJob);
            var entities = new NativeArray<Entity>(count, Allocator.TempJob);
            var sections = new NativeArray<SectionReference>(count, Allocator.TempJob);
            var styleData = new NativeArray<TrackStyle>(count, Allocator.TempJob);
            var segments = new NativeArray<Segment>(count, Allocator.TempJob);

            int index = 0;
            foreach (var (section, segment, entity) in SystemAPI
                .Query<SectionReference, Segment>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasComponent<TrackStyle>(segment.Style)) continue;

                var style = SystemAPI.GetComponent<TrackStyle>(segment.Style);
                entities[index] = entity;
                sections[index] = section;
                styleData[index] = style;
                segments[index] = segment;
                index++;
            }

            var countJob = new CalculateCountJob {
                Entities = entities,
                Sections = sections,
                StyleData = styleData,
                Segments = segments,
                PointLookup = _pointLookup,
                CountMap = countMap.AsParallelWriter()
            };

            var countHandle = countJob.Schedule(count, 32, state.Dependency);

            using var disposeJobs = new NativeArray<JobHandle>(4, Allocator.Temp) {
                [0] = entities.Dispose(countHandle),
                [1] = sections.Dispose(countHandle),
                [2] = styleData.Dispose(countHandle),
                [3] = segments.Dispose(countHandle)
            };
            var disposalHandle = JobHandle.CombineDependencies(disposeJobs);

            state.Dependency = new BuildJob {
                PointLookup = _pointLookup,
                TrackPointLookup = _trackPointLookup,
                CountMap = countMap,
                VisualizationMode = preferences.VisualizationMode
            }.ScheduleParallel(_query, countHandle);

            state.Dependency = countMap.Dispose(state.Dependency);
        }

        [BurstCompile]
        private struct CalculateCountJob : IJobParallelFor {
            [ReadOnly]
            public NativeArray<Entity> Entities;
            [ReadOnly]
            public NativeArray<SectionReference> Sections;
            [ReadOnly]
            public NativeArray<TrackStyle> StyleData;
            [ReadOnly]
            public NativeArray<Segment> Segments;
            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public NativeParallelHashMap<Entity, int>.ParallelWriter CountMap;

            public void Execute(int index) {
                var entity = Entities[index];
                if (entity == Entity.Null) return;

                var section = Sections[index];
                var style = StyleData[index];
                var segment = Segments[index];

                if (!PointLookup.HasBuffer(section)) {
                    CountMap.TryAdd(entity, 2);
                    return;
                }

                var points = PointLookup[section];
                if (points.Length < 2) {
                    CountMap.TryAdd(entity, 2);
                    return;
                }

                var (startIndex, endIndex) = GetPointRange(points, segment);
                if (endIndex <= startIndex) {
                    CountMap.TryAdd(entity, 2);
                    return;
                }

                float trackLength = points[endIndex].Value.TotalLength - points[startIndex].Value.TotalLength;
                if (trackLength <= 0f) {
                    CountMap.TryAdd(entity, 2);
                    return;
                }

                float nominalCount = trackLength / style.Spacing;
                int baseCount = math.max(2, (int)math.round(nominalCount));

                int finalCount = style.Step == 1 ? baseCount : math.max(style.Step, (baseCount / style.Step) * style.Step);
                CountMap.TryAdd(entity, finalCount);
            }

            private (int startIndex, int endIndex) GetPointRange(DynamicBuffer<Point> points, Segment segment) {
                if (points.Length < 2) {
                    return (0, 0);
                }

                int startIndex = 0;
                int endIndex = points.Length - 1;

                if (segment.StartTime > 0f) {
                    float targetStartTime = segment.StartTime;
                    int left = 0;
                    int right = points.Length - 1;

                    while (left <= right) {
                        int mid = left + (right - left) / 2;
                        float pointTime = mid / HZ;

                        if (pointTime < targetStartTime) {
                            left = mid + 1;
                        }
                        else {
                            startIndex = mid;
                            right = mid - 1;
                        }
                    }
                }

                if (segment.EndTime < float.MaxValue) {
                    float targetEndTime = segment.EndTime;
                    int left = startIndex;
                    int right = points.Length - 1;

                    while (left <= right) {
                        int mid = left + (right - left) / 2;
                        float pointTime = mid / HZ;

                        if (pointTime < targetEndTime) {
                            left = mid + 1;
                        }
                        else {
                            endIndex = mid;
                            right = mid - 1;
                        }
                    }

                    endIndex = math.max(startIndex + 1, endIndex);
                }

                return (startIndex, endIndex);
            }
        }

        [BurstCompile]
        private partial struct BuildJob : IJobEntity {
            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            [NativeDisableParallelForRestriction]
            public BufferLookup<TrackPoint> TrackPointLookup;

            [ReadOnly]
            public NativeParallelHashMap<Entity, int> CountMap;

            [ReadOnly]
            public VisualizationMode VisualizationMode;

            public void Execute(Entity entity, ref TrackHash trackHash, in SectionReference section, in Segment segment) {
                if (!PointLookup.HasBuffer(section)) return;

                var points = PointLookup[section];
                var trackPoints = TrackPointLookup[entity];
                trackPoints.Clear();
                trackHash = 0;

                if (points.Length < 2) return;

                if (!CountMap.TryGetValue(entity, out int desiredCount) ||
                    desiredCount < 2) return;

                var (startIndex, endIndex) = GetPointRange(points, segment);
                if (endIndex <= startIndex) return;

                PointData first = points[startIndex].Value;
                PointData last = points[endIndex].Value;
                float startLength = first.TotalLength;
                float endLength = last.TotalLength;
                float totalLength = endLength - startLength;

                int currentSegment = startIndex;
                for (int i = 0; i <= desiredCount; i++) {
                    float t = i / (float)desiredCount;
                    float targetLength = startLength + t * totalLength;

                    while (currentSegment < endIndex - 1 && points[currentSegment + 1].Value.TotalLength < targetLength) {
                        currentSegment++;
                    }

                    PointData p0 = points[currentSegment].Value;
                    PointData p1 = points[currentSegment + 1].Value;

                    float segmentT = math.saturate((targetLength - p0.TotalLength) / (p1.TotalLength - p0.TotalLength));

                    float3 position = math.lerp(p0.GetHeartPosition(p0.Heart), p1.GetHeartPosition(p1.Heart), segmentT);
                    float3 direction = math.lerp(p0.GetHeartDirection(p0.Heart), p1.GetHeartDirection(p1.Heart), segmentT);
                    float3 lateral = math.lerp(p0.GetHeartLateral(p0.Heart), p1.GetHeartLateral(p1.Heart), segmentT);
                    float3 normal = -math.normalize(math.cross(direction, lateral));
                    float distance = targetLength - startLength;
                    float heart = math.lerp(p0.Heart, p1.Heart, segmentT);
                    float time = segment.StartTime + t * (segment.EndTime == float.MaxValue ? (endIndex - startIndex) / HZ : segment.EndTime - segment.StartTime);

                    float visualizationValue = GetVisualizationValue(p0, p1, segmentT);

                    trackPoints.Add(new TrackPoint {
                        Position = position,
                        Direction = direction,
                        Normal = normal,
                        Distance = distance,
                        Heart = heart,
                        Time = time,
                        VisualizationValue = visualizationValue,
                    });

                    trackHash = math.hash(new float4(position, trackHash));
                }
            }

            private static (int startIndex, int endIndex) GetPointRange(DynamicBuffer<Point> points, Segment segment) {
                if (points.Length < 2) {
                    return (0, 0);
                }

                int startIndex = 0;
                int endIndex = points.Length - 1;

                if (segment.StartTime > 0f) {
                    float targetStartTime = segment.StartTime;
                    int left = 0;
                    int right = points.Length - 1;

                    while (left <= right) {
                        int mid = left + (right - left) / 2;
                        float pointTime = mid / HZ;

                        if (pointTime < targetStartTime) {
                            left = mid + 1;
                        }
                        else {
                            startIndex = mid;
                            right = mid - 1;
                        }
                    }
                }

                if (segment.EndTime < float.MaxValue) {
                    float targetEndTime = segment.EndTime;
                    int left = startIndex;
                    int right = points.Length - 1;

                    while (left <= right) {
                        int mid = left + (right - left) / 2;
                        float pointTime = mid / HZ;

                        if (pointTime < targetEndTime) {
                            left = mid + 1;
                        }
                        else {
                            endIndex = mid;
                            right = mid - 1;
                        }
                    }

                    endIndex = math.max(startIndex + 1, endIndex);
                }

                return (startIndex, endIndex);
            }

            private float GetVisualizationValue(PointData p0, PointData p1, float t) {
                return VisualizationMode switch {
                    VisualizationMode.Velocity => math.lerp(p0.Velocity, p1.Velocity, t),
                    VisualizationMode.NormalForce => math.lerp(p0.NormalForce, p1.NormalForce, t),
                    VisualizationMode.LateralForce => math.lerp(p0.LateralForce, p1.LateralForce, t),
                    VisualizationMode.RollSpeed => math.lerp(p0.RollSpeed, p1.RollSpeed, t),
                    VisualizationMode.PitchSpeed => math.lerp(p0.PitchFromLast, p1.PitchFromLast, t),
                    VisualizationMode.YawSpeed => math.lerp(p0.YawFromLast, p1.YawFromLast, t),
                    VisualizationMode.Curvature => math.lerp(p0.AngleFromLast, p1.AngleFromLast, t),
                    _ => math.lerp(p0.Velocity, p1.Velocity, t),
                };
            }
        }
    }
}
