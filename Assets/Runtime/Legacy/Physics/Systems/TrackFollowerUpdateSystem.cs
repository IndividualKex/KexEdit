using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct TrackFollowerUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            state.Dependency = new Job {
                PointLookup = SystemAPI.GetBufferLookup<CorePointBuffer>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public BufferLookup<CorePointBuffer> PointLookup;

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

            private float3 GetPosition(DynamicBuffer<CorePointBuffer> points, int index, float t) {
                var point = points[index];
                var next = points[index + 1];
                return math.lerp(
                    point.GetSpinePosition(point.HeartOffset()),
                    next.GetSpinePosition(next.HeartOffset()),
                    t
                );
            }

            private quaternion GetRotation(DynamicBuffer<CorePointBuffer> points, int index, float t) {
                var point = points[index];
                var next = points[index + 1];
                int facing = point.Facing;
                float3 direction = math.normalize(math.lerp(
                    point.GetSpineDirection(point.HeartOffset()),
                    next.GetSpineDirection(next.HeartOffset()),
                    t
                ));
                float3 lateral = math.normalize(math.lerp(
                    point.GetSpineLateral(point.HeartOffset()),
                    next.GetSpineLateral(next.HeartOffset()),
                    t
                ));
                float3 normal = math.normalize(math.cross(direction, lateral));

                float3 finalDirection = facing > 0 ? -direction : direction;
                return quaternion.LookRotation(finalDirection, -normal);
            }

            private void HandleOutOfBounds(in TrackFollower follower, DynamicBuffer<CorePointBuffer> points, ref LocalTransform transform) {
                CorePointBuffer edgePoint;
                float3 projectionDirection;

                if (follower.Index <= 0f) {
                    edgePoint = points[0];
                    projectionDirection = -math.normalize(edgePoint.GetSpineDirection(edgePoint.HeartOffset()));
                }
                else {
                    edgePoint = points[^1];
                    projectionDirection = math.normalize(edgePoint.GetSpineDirection(edgePoint.HeartOffset()));
                }

                float3 edgePosition = edgePoint.GetSpinePosition(edgePoint.HeartOffset());
                float3 position = edgePosition + projectionDirection * follower.ProjectionDistance;

                float3 direction = math.normalize(edgePoint.GetSpineDirection(edgePoint.HeartOffset()));
                float3 lateral = math.normalize(edgePoint.GetSpineLateral(edgePoint.HeartOffset()));
                float3 normal = math.normalize(math.cross(direction, lateral));

                float3 finalDirection = edgePoint.Facing > 0 ? -direction : direction;
                quaternion rotation = quaternion.LookRotation(finalDirection, -normal);

                transform = LocalTransform.FromPositionRotation(position, rotation);
            }
        }
    }
}
