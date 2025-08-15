using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct TrackFollowerUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            state.Dependency = new Job {
                PointLookup = SystemAPI.GetBufferLookup<Point>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(in TrackFollower follower, ref LocalTransform transform) {
                if (!follower.Active || !PointLookup.TryGetBuffer(follower.Section, out var points) || points.Length < 2) {
                    transform = LocalTransform.FromPosition(new float3(0f, -999f, 0f));
                    return;
                }

                if (follower.OutOfBounds) {
                    HandleOutOfBounds(follower, points, ref transform);
                    return;
                }

                if (follower.Index < 0f) return;

                if (follower.Index >= points.Length - 1f) {
                    int lastIndex = points.Length - 2;
                    float3 lastPosition = GetPosition(points, lastIndex, 1f);
                    quaternion lastRotation = GetRotation(points, lastIndex, 1f);
                    transform = LocalTransform.FromPositionRotation(lastPosition, lastRotation);
                    return;
                }

                int index = (int)math.floor(follower.Index);
                float t = follower.Index - index;

                float3 position = GetPosition(points, index, t);
                quaternion rotation = GetRotation(points, index, t);

                transform = LocalTransform.FromPositionRotation(position, rotation);
            }

            private float3 GetPosition(DynamicBuffer<Point> points, int index, float t) {
                PointData point = points[index].Value;
                PointData next = points[index + 1].Value;
                return math.lerp(
                    point.GetHeartPosition(point.Heart),
                    next.GetHeartPosition(next.Heart),
                    t
                );
            }

            private quaternion GetRotation(DynamicBuffer<Point> points, int index, float t) {
                PointData point = points[index].Value;
                PointData next = points[index + 1].Value;
                int facing = point.Facing;
                float3 direction = math.normalize(math.lerp(
                    point.GetHeartDirection(point.Heart),
                    next.GetHeartDirection(next.Heart),
                    t
                ));
                float3 lateral = math.normalize(math.lerp(
                    point.GetHeartLateral(point.Heart),
                    next.GetHeartLateral(next.Heart),
                    t
                ));
                float3 normal = math.normalize(math.cross(direction, lateral));

                float3 finalDirection = facing > 0 ? -direction : direction;
                return quaternion.LookRotation(finalDirection, -normal);
            }

            private void HandleOutOfBounds(in TrackFollower follower, DynamicBuffer<Point> points, ref LocalTransform transform) {
                PointData edgePoint;
                float3 projectionDirection;

                if (follower.Index <= 0f) {
                    edgePoint = points[0].Value;
                    projectionDirection = -math.normalize(edgePoint.GetHeartDirection(edgePoint.Heart));
                }
                else {
                    edgePoint = points[^1].Value;
                    projectionDirection = math.normalize(edgePoint.GetHeartDirection(edgePoint.Heart));
                }

                float3 edgePosition = edgePoint.GetHeartPosition(edgePoint.Heart);
                float3 position = edgePosition + projectionDirection * follower.ProjectionDistance;

                float3 direction = math.normalize(edgePoint.GetHeartDirection(edgePoint.Heart));
                float3 lateral = math.normalize(edgePoint.GetHeartLateral(edgePoint.Heart));
                float3 normal = math.normalize(math.cross(direction, lateral));

                float3 finalDirection = edgePoint.Facing > 0 ? -direction : direction;
                quaternion rotation = quaternion.LookRotation(finalDirection, -normal);

                transform = LocalTransform.FromPositionRotation(position, rotation);
            }
        }
    }
}
