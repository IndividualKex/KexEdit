using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CartSystem : ISystem {
        private BufferLookup<Point> _pointLookup;
        private ComponentLookup<Node> _nodeLookup;

        public void OnCreate(ref SystemState state) {
            _pointLookup = SystemAPI.GetBufferLookup<Point>(true);
            _nodeLookup = SystemAPI.GetComponentLookup<Node>(true);
        }

        public void OnUpdate(ref SystemState state) {
            _pointLookup.Update(ref state);
            _nodeLookup.Update(ref state);

            Entity rootEntity = Entity.Null;
            if (SystemAPI.HasSingleton<NodeGraphRoot>()) {
                rootEntity = SystemAPI.GetSingleton<NodeGraphRoot>().Value;
            }

            state.Dependency = new Job {
                PointLookup = _pointLookup,
                NodeLookup = _nodeLookup,
                RootEntity = rootEntity,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Paused = KexTime.IsPaused,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            [ReadOnly]
            public ComponentLookup<Node> NodeLookup;

            [ReadOnly]
            public Entity RootEntity;

            [ReadOnly]
            public float DeltaTime;

            [ReadOnly]
            public bool Paused;

            public void Execute(ref Cart cart, ref LocalTransform transform) {
                if (!cart.Active || RootEntity == Entity.Null) {
                    transform.Position = new float3(0f, -999f, 0f);
                    return;
                }

                if (cart.Section == Entity.Null ||
                    !PointLookup.TryGetBuffer(cart.Section, out var points) ||
                    points.Length < 2) {
                    Reset(ref cart);
                    return;
                }

                if (!cart.Kinematic && !Paused) {
                    cart.Position += DeltaTime * HZ;
                    if (cart.Position > points.Length - 1) {
                        float overshoot = cart.Position - (points.Length - 1);

                        if (NodeLookup.TryGetComponent(cart.Section, out var node) && node.Next != Entity.Null) {
                            cart.Section = node.Next;
                            cart.Position = overshoot;
                            points = PointLookup[cart.Section];
                        }
                        else {
                            Reset(ref cart);
                            return;
                        }
                    }
                }

                cart.Position = math.clamp(cart.Position, 0f, points.Length - 1f);
                int frontIndex = (int)math.floor(cart.Position);
                float t = cart.Position - frontIndex;

                if (frontIndex >= points.Length - 1) {
                    frontIndex = points.Length - 2;
                    t = 1f;
                }

                const float BACK_AXLE_OFFSET = 1.9f;
                int facing = points[frontIndex].Value.Facing;
                float distance = points[frontIndex].Value.TotalLength;
                float backDistance = facing > 0 ? distance - BACK_AXLE_OFFSET : distance + BACK_AXLE_OFFSET;
                int backIndex = frontIndex;
                Entity backSection = cart.Section;
                var backPoints = points;

                PointData frontPoint = points[frontIndex].Value;
                float3 frontDirection = frontPoint.GetHeartDirection(frontPoint.Heart);

                if (facing > 0) {
                    while (distance > backDistance) {
                        if (backIndex <= 0) {
                            if (NodeLookup.TryGetComponent(backSection, out var backNode) && backNode.Previous != Entity.Null) {
                                var nextPoints = PointLookup[backNode.Previous];
                                if (nextPoints.Length < 2) break;

                                PointData nextPoint = nextPoints[^2].Value;
                                float3 nextDirection = nextPoint.GetHeartDirection(nextPoint.Heart);
                                if (math.dot(frontDirection, nextDirection) < 0f) break;

                                backSection = backNode.Previous;
                                backPoints = nextPoints;
                                backIndex = backPoints.Length - 2;
                            }
                            else {
                                break;
                            }
                        }
                        backIndex--;
                        distance = backPoints[backIndex].Value.TotalLength;
                    }
                }
                else {
                    while (distance < backDistance) {
                        if (backIndex >= backPoints.Length - 2) {
                            if (NodeLookup.TryGetComponent(backSection, out var backNode) && backNode.Next != Entity.Null) {
                                var nextPoints = PointLookup[backNode.Next];
                                if (nextPoints.Length == 0) break;

                                PointData nextPoint = nextPoints[0].Value;
                                float3 nextDirection = nextPoint.GetHeartDirection(nextPoint.Heart);
                                if (math.dot(frontDirection, nextDirection) < 0f) break;

                                backSection = backNode.Next;
                                backPoints = nextPoints;
                                backIndex = 0;
                            }
                            else {
                                break;
                            }
                        }
                        backIndex++;
                        distance = backPoints[backIndex].Value.TotalLength;
                    }
                }

                float3 frontPosition = GetPosition(points, frontIndex, t);
                float3 backPosition = GetPosition(backPoints, backIndex, t);
                quaternion frontRotation = GetRotation(points, frontIndex, t);

                transform.Position = frontPosition;

                float3 vec = backPosition - frontPosition;
                if (math.lengthsq(vec) > 1e-6f) {
                    float3 direction = math.normalize(vec);
                    float3 up = math.mul(frontRotation, math.up());
                    transform.Rotation = quaternion.LookRotation(direction, up);
                }
                else {
                    transform.Rotation = frontRotation;
                }
            }

            private void Reset(ref Cart cart) {
                cart.Section = RootEntity;
                cart.Position = 1f;
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
        }
    }
}
