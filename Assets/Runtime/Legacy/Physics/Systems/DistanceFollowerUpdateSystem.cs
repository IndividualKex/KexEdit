using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrainUpdateSystem))]
    [UpdateBefore(typeof(TrackFollowerUpdateSystem))]
    [BurstCompile]
    public partial struct DistanceFollowerUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            state.Dependency = new Job {
                CoasterLookup = SystemAPI.GetComponentLookup<Coaster>(true),
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                PointLookup = SystemAPI.GetBufferLookup<Point>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public ComponentLookup<Coaster> CoasterLookup;
            [ReadOnly]
            public ComponentLookup<Node> NodeLookup;
            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            public void Execute(in CoasterReference coasterReference, in DistanceFollower distanceFollower, ref TrackFollower follower) {
                if (!distanceFollower.Active ||
                    !CoasterLookup.TryGetComponent(coasterReference.Value, out var coaster) ||
                    coaster.RootNode == Entity.Null) {
                    follower.Active = false;
                    return;
                }

                float distance = distanceFollower.Distance;
                Entity node = GetCurrentNode(coaster.RootNode, distance, out float projectionDistance, out bool outOfBounds);
                if (node == Entity.Null) {
                    follower.Active = false;
                    return;
                }

                follower.Section = node;
                follower.Index = GetIndex(node, distance);
                follower.ProjectionDistance = projectionDistance;
                follower.OutOfBounds = outOfBounds;
                follower.Active = true;
            }

            private Entity GetCurrentNode(Entity entity, float targetDistance, out float projectionDistance, out bool outOfBounds) {
                Entity firstNode = GetFirstNode(entity);
                Entity lastNode = GetLastNode(entity);

                if (!PointLookup.TryGetBuffer(firstNode, out var firstPoints) || firstPoints.Length < 2) {
                    projectionDistance = 0f;
                    outOfBounds = false;
                    return GetCurrentNode(entity, targetDistance);
                }

                if (!PointLookup.TryGetBuffer(lastNode, out var lastPoints) || lastPoints.Length < 2) {
                    projectionDistance = 0f;
                    outOfBounds = false;
                    return GetCurrentNode(entity, targetDistance);
                }

                float trackStart = firstPoints[0].Value.TotalLength;
                float trackEnd = lastPoints[^1].Value.TotalLength;

                if (targetDistance < trackStart) {
                    projectionDistance = trackStart - targetDistance;
                    outOfBounds = true;
                    return firstNode;
                }

                if (targetDistance > trackEnd) {
                    projectionDistance = targetDistance - trackEnd;
                    outOfBounds = true;
                    return lastNode;
                }

                projectionDistance = 0f;
                outOfBounds = false;
                return GetCurrentNode(entity, targetDistance);
            }

            private Entity GetCurrentNode(Entity entity, float targetDistance) {
                if (!NodeLookup.TryGetComponent(entity, out var node)) {
                    return entity;
                }

                if (node.Next == Entity.Null) {
                    return entity;
                }

                if (!PointLookup.TryGetBuffer(entity, out var points) || points.Length < 2) {
                    return GetCurrentNode(node.Next, targetDistance);
                }

                float endLength = points[^1].Value.TotalLength;
                if (targetDistance < endLength) {
                    return entity;
                }

                return GetCurrentNode(node.Next, targetDistance);
            }

            private Entity GetFirstNode(Entity entity) {
                if (!NodeLookup.TryGetComponent(entity, out var node) || node.Previous == Entity.Null) {
                    return entity;
                }
                return GetFirstNode(node.Previous);
            }

            private Entity GetLastNode(Entity entity) {
                if (!NodeLookup.TryGetComponent(entity, out var node) || node.Next == Entity.Null) {
                    return entity;
                }
                return GetLastNode(node.Next);
            }

            private float GetIndex(Entity entity, float targetDistance) {
                if (!PointLookup.TryGetBuffer(entity, out var points) || points.Length < 2) {
                    return 0f;
                }

                if (targetDistance < points[0].Value.TotalLength) {
                    return 0f;
                }

                for (int i = 0; i < points.Length - 1; i++) {
                    float startLength = points[i].Value.TotalLength;
                    float endLength = points[i + 1].Value.TotalLength;
                    if (targetDistance >= startLength && targetDistance < endLength) {
                        float t = (endLength - startLength) > 0 ?
                            (targetDistance - startLength) / (endLength - startLength) : 0f;
                        return i + t;
                    }
                }

                return points.Length - 1;
            }
        }
    }
}
