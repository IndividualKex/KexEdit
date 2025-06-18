using Unity.Mathematics;
using static KexEdit.Constants;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct TrackPointSystem : ISystem {
        private BufferLookup<Point> _pointLookup;
        private BufferLookup<TrackPoint> _trackPointLookup;

        private EntityQuery _query;

        public void OnCreate(ref SystemState state) {
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);
            _trackPointLookup = SystemAPI.GetBufferLookup<TrackPoint>(false);

            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Point, TrackPoint, TrackHash>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state) {
            _pointLookup.Update(ref state);
            _trackPointLookup.Update(ref state);

            state.Dependency = new Job {
                PointLookup = _pointLookup,
                TrackPointLookup = _trackPointLookup,
                Step = 2,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            [NativeDisableParallelForRestriction]
            public BufferLookup<TrackPoint> TrackPointLookup;

            [ReadOnly]
            public int Step;

            public void Execute(Entity entity, ref TrackHash trackHash) {
                var points = PointLookup[entity];
                var trackPoints = TrackPointLookup[entity];
                trackPoints.Clear();
                trackHash = 0;

                if (points.Length < 2) return;

                PointData first = points[0];
                PointData last = points[^1];

                float sectionLength = last.TotalLength - first.TotalLength;
                int numPoints = math.max(1, (int)math.round(sectionLength / TRACK_POINT_HZ / Step) * Step);
                float adjustedSpacing = sectionLength / numPoints;

                float startLength = first.TotalLength;
                float nextLength = startLength;

                for (int i = 0; i < points.Length - 1; i++) {
                    PointData p0 = points[i];
                    PointData p1 = points[i + 1];

                    float start = p0.TotalLength;
                    float end = p1.TotalLength;

                    while (nextLength <= end) {
                        float t = math.saturate((nextLength - start) / (end - start));

                        float3 position = math.lerp(p0.GetHeartPosition(p0.Heart), p1.GetHeartPosition(p1.Heart), t);
                        float3 direction = math.lerp(p0.GetHeartDirection(p0.Heart), p1.GetHeartDirection(p1.Heart), t);
                        float3 lateral = math.lerp(p0.GetHeartLateral(p0.Heart), p1.GetHeartLateral(p1.Heart), t);
                        float3 normal = -math.normalize(math.cross(direction, lateral));
                        float velocity = math.lerp(p0.Velocity, p1.Velocity, t);
                        float distance = math.lerp(p0.TotalLength, p1.TotalLength, t) - startLength;
                        float time = (i + t) / HZ;

                        trackPoints.Add(new TrackPoint {
                            Position = position,
                            Direction = direction,
                            Normal = normal,
                            Velocity = velocity,
                            Distance = distance,
                            Time = time,
                        });

                        nextLength += adjustedSpacing;

                        trackHash = math.hash(new float4(position, trackHash));
                    }
                }

                float3 lastPosition = last.GetHeartPosition(last.Heart);
                float3 lastDirection = last.GetHeartDirection(last.Heart);
                float3 lastLateral = last.GetHeartLateral(last.Heart);
                float3 lastNormal = -math.normalize(math.cross(lastDirection, lastLateral));

                trackPoints.Add(new TrackPoint {
                    Position = lastPosition,
                    Direction = lastDirection,
                    Normal = lastNormal,
                    Velocity = last.Velocity,
                    Distance = last.TotalLength - startLength,
                    Time = points.Length / HZ,
                });

                trackHash = math.hash(new float4(lastPosition, trackHash));
            }
        }
    }
}
