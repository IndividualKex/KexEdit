using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial class TrackPointSystem : SystemBase {
        private BufferLookup<Point> _pointLookup;
        private BufferLookup<TrackPoint> _trackPointLookup;

        private EntityQuery _query;

        protected override void OnCreate() {
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);
            _trackPointLookup = SystemAPI.GetBufferLookup<TrackPoint>(false);

            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SectionReference, TrackPoint, TrackHash, TrackStyle, Segment>()
                .Build(EntityManager);

            RequireForUpdate(_query);
        }

        protected override void OnUpdate() {
            _pointLookup.Update(this);
            _trackPointLookup.Update(this);

            int count = _query.CalculateEntityCount();
            var countMap = new NativeParallelHashMap<Entity, int>(count, Allocator.TempJob);
            foreach (var (section, style, segment, entity) in SystemAPI
                .Query<SectionReference, TrackStyle, Segment>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasBuffer<Point>(section)) continue;
                int trackPointCount = CalculateTrackPointCount(section, style, segment);
                countMap.Add(entity, trackPointCount);
            }

            Dependency = new BuildJob {
                PointLookup = _pointLookup,
                TrackPointLookup = _trackPointLookup,
                CountMap = countMap,
                VisualizationMode = VisualizationSystem.CurrentMode
            }.ScheduleParallel(_query, Dependency);

            Dependency = countMap.Dispose(Dependency);
        }

        private int CalculateTrackPointCount(Entity section, TrackStyle style, Segment segment) {
            if (!SystemAPI.HasBuffer<Point>(section)) {
                return 2;
            }

            var points = SystemAPI.GetBuffer<Point>(section);
            if (points.Length < 2) {
                return 2;
            }

            var (startIndex, endIndex) = GetPointRange(points, segment);
            if (endIndex <= startIndex) {
                return 2;
            }

            float trackLength = points[endIndex].Value.TotalLength - points[startIndex].Value.TotalLength;
            if (trackLength <= 0f) {
                return 2;
            }

            int lcm = 1;
            foreach (var duplicationMesh in style.DuplicationMeshes) {
                lcm = LCM(lcm, duplicationMesh.Step);
            }

            float nominalCount = trackLength / style.Spacing;
            int baseCount = math.max(2, (int)math.round(nominalCount));

            if (lcm == 1) {
                return baseCount;
            }

            return math.max(lcm, (baseCount / lcm) * lcm);
        }

        private (int startIndex, int endIndex) GetPointRange(DynamicBuffer<Point> points, Segment segment) {
            if (points.Length < 2) {
                return (0, 0);
            }

            int startIndex = 0;
            int endIndex = points.Length - 1;

            if (segment.StartTime > 0f) {
                float targetStartTime = segment.StartTime;
                for (int i = 0; i < points.Length; i++) {
                    float pointTime = i / HZ;
                    if (pointTime >= targetStartTime) {
                        startIndex = i;
                        break;
                    }
                }
            }

            if (segment.EndTime < float.MaxValue) {
                float targetEndTime = segment.EndTime;
                for (int i = startIndex; i < points.Length; i++) {
                    float pointTime = i / HZ;
                    if (pointTime >= targetEndTime) {
                        endIndex = math.max(startIndex + 1, i);
                        break;
                    }
                }
            }

            return (startIndex, endIndex);
        }

        [BurstCompile]
        private static int GCD(int a, int b) {
            while (b != 0) {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        [BurstCompile]
        private static int LCM(int a, int b) {
            return (a * b) / GCD(a, b);
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

                int desiredCount = CountMap[entity];
                if (desiredCount < 2) return;

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

            private (int startIndex, int endIndex) GetPointRange(DynamicBuffer<Point> points, Segment segment) {
                if (points.Length < 2) {
                    return (0, 0);
                }

                int startIndex = 0;
                int endIndex = points.Length - 1;

                if (segment.StartTime > 0f) {
                    float targetStartTime = segment.StartTime;
                    for (int i = 0; i < points.Length; i++) {
                        float pointTime = i / HZ;
                        if (pointTime >= targetStartTime) {
                            startIndex = i;
                            break;
                        }
                    }
                }

                if (segment.EndTime < float.MaxValue) {
                    float targetEndTime = segment.EndTime;
                    for (int i = startIndex; i < points.Length; i++) {
                        float pointTime = i / HZ;
                        if (pointTime >= targetEndTime) {
                            endIndex = math.max(startIndex + 1, i);
                            break;
                        }
                    }
                }

                return (startIndex, endIndex);
            }

            private float GetVisualizationValue(PointData p0, PointData p1, float t) {
                switch (VisualizationMode) {
                    case VisualizationMode.Velocity:
                        return math.lerp(p0.Velocity, p1.Velocity, t);
                    case VisualizationMode.NormalForce:
                        return math.lerp(p0.NormalForce, p1.NormalForce, t);
                    case VisualizationMode.LateralForce:
                        return math.lerp(p0.LateralForce, p1.LateralForce, t);
                    case VisualizationMode.RollSpeed:
                        return math.lerp(p0.RollSpeed, p1.RollSpeed, t);
                    case VisualizationMode.PitchSpeed:
                        return math.lerp(math.abs(p0.PitchFromLast), math.abs(p1.PitchFromLast), t);
                    case VisualizationMode.YawSpeed:
                        return math.lerp(math.abs(p0.YawFromLast), math.abs(p1.YawFromLast), t);
                    case VisualizationMode.Curvature:
                        return math.lerp(math.abs(p0.AngleFromLast), math.abs(p1.AngleFromLast), t);
                    default:
                        return math.lerp(p0.Velocity, p1.Velocity, t);
                }
            }
        }
    }
}
