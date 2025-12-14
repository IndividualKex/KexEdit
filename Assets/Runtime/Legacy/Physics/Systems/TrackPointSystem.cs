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
        private EntityQuery _query;
        private EntityQuery _countQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
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
                PointLookup = SystemAPI.GetBufferLookup<Point>(true),
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
                PointLookup = SystemAPI.GetBufferLookup<Point>(true),
                ReadNormalForceLookup = SystemAPI.GetBufferLookup<ReadNormalForce>(true),
                ReadLateralForceLookup = SystemAPI.GetBufferLookup<ReadLateralForce>(true),
                ReadRollSpeedLookup = SystemAPI.GetBufferLookup<ReadRollSpeed>(true),
                ReadPitchSpeedLookup = SystemAPI.GetBufferLookup<ReadPitchSpeed>(true),
                ReadYawSpeedLookup = SystemAPI.GetBufferLookup<ReadYawSpeed>(true),
                TrackPointLookup = SystemAPI.GetBufferLookup<TrackPoint>(false),
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

                int lastIndex = points.Length - 1;
                int startIndex = 0;
                int endIndex = lastIndex;

                if (segment.StartTime > 0f) {
                    float targetStartTime = segment.StartTime;
                    int left = 0;
                    int right = lastIndex;

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

                    startIndex = math.min(startIndex, lastIndex - 1);
                }

                if (segment.EndTime < float.MaxValue) {
                    float targetEndTime = segment.EndTime;
                    int left = startIndex;
                    int right = lastIndex;

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

                    endIndex = math.clamp(endIndex, startIndex + 1, lastIndex);
                }
                else {
                    endIndex = math.min(lastIndex, math.max(startIndex + 1, endIndex));
                }

                return (startIndex, endIndex);
            }
        }

        [BurstCompile]
        private partial struct BuildJob : IJobEntity {
            [ReadOnly]
            public BufferLookup<Point> PointLookup;
            [ReadOnly]
            public BufferLookup<ReadNormalForce> ReadNormalForceLookup;
            [ReadOnly]
            public BufferLookup<ReadLateralForce> ReadLateralForceLookup;
            [ReadOnly]
            public BufferLookup<ReadRollSpeed> ReadRollSpeedLookup;
            [ReadOnly]
            public BufferLookup<ReadPitchSpeed> ReadPitchSpeedLookup;
            [ReadOnly]
            public BufferLookup<ReadYawSpeed> ReadYawSpeedLookup;

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

                    float visualizationValue = GetVisualizationValue(section, currentSegment, p0, p1, segmentT);

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

                int lastIndex = points.Length - 1;
                int startIndex = 0;
                int endIndex = lastIndex;

                if (segment.StartTime > 0f) {
                    float targetStartTime = segment.StartTime;
                    int left = 0;
                    int right = lastIndex;

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

                    startIndex = math.min(startIndex, lastIndex - 1);
                }

                if (segment.EndTime < float.MaxValue) {
                    float targetEndTime = segment.EndTime;
                    int left = startIndex;
                    int right = lastIndex;

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

                    endIndex = math.clamp(endIndex, startIndex + 1, lastIndex);
                }
                else {
                    endIndex = math.min(lastIndex, math.max(startIndex + 1, endIndex));
                }

                return (startIndex, endIndex);
            }

            private float GetVisualizationValue(Entity section, int index, PointData p0, PointData p1, float t) {
                float value0, value1;

                switch (VisualizationMode) {
                    case VisualizationMode.Velocity:
                        return math.lerp(p0.Velocity, p1.Velocity, t);

                    case VisualizationMode.NormalForce:
                        if (ReadNormalForceLookup.HasBuffer(section)) {
                            var buffer = ReadNormalForceLookup[section];
                            if (index < buffer.Length - 1) {
                                value0 = buffer[index].Value;
                                value1 = buffer[index + 1].Value;
                                return math.lerp(value0, value1, t) - 1f;
                            }
                        }
                        return math.lerp(p0.NormalForce, p1.NormalForce, t) - 1f;

                    case VisualizationMode.LateralForce:
                        if (ReadLateralForceLookup.HasBuffer(section)) {
                            var buffer = ReadLateralForceLookup[section];
                            if (index < buffer.Length - 1) {
                                value0 = buffer[index].Value;
                                value1 = buffer[index + 1].Value;
                                return math.lerp(value0, value1, t);
                            }
                        }
                        return math.lerp(p0.LateralForce, p1.LateralForce, t);

                    case VisualizationMode.RollSpeed:
                        if (ReadRollSpeedLookup.HasBuffer(section)) {
                            var buffer = ReadRollSpeedLookup[section];
                            if (index < buffer.Length - 1) {
                                value0 = buffer[index].Value;
                                value1 = buffer[index + 1].Value;
                                return math.lerp(value0, value1, t);
                            }
                        }
                        return math.lerp(p0.RollSpeed, p1.RollSpeed, t);

                    case VisualizationMode.PitchSpeed:
                        if (ReadPitchSpeedLookup.HasBuffer(section)) {
                            var buffer = ReadPitchSpeedLookup[section];
                            if (index < buffer.Length - 1) {
                                value0 = buffer[index].Value;
                                value1 = buffer[index + 1].Value;
                                return math.lerp(value0, value1, t);
                            }
                        }
                        return math.lerp(p0.PitchFromLast, p1.PitchFromLast, t);

                    case VisualizationMode.YawSpeed:
                        if (ReadYawSpeedLookup.HasBuffer(section)) {
                            var buffer = ReadYawSpeedLookup[section];
                            if (index < buffer.Length - 1) {
                                value0 = buffer[index].Value;
                                value1 = buffer[index + 1].Value;
                                return math.lerp(value0, value1, t);
                            }
                        }
                        return math.lerp(p0.YawFromLast, p1.YawFromLast, t);

                    case VisualizationMode.Curvature:
                        return math.lerp(p0.AngleFromLast, p1.AngleFromLast, t);

                    default:
                        return math.lerp(p0.Velocity, p1.Velocity, t);
                }
            }
        }
    }
}
