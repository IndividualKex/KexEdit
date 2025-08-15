using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct TrainSystem : ISystem {
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<PauseSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            bool paused = SystemAPI.GetSingleton<PauseSingleton>().IsPaused;

            state.Dependency = new TrainJob {
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                CoasterLookup = SystemAPI.GetComponentLookup<Coaster>(true),
                PointLookup = SystemAPI.GetBufferLookup<Point>(true),
                TrainCarLookup = SystemAPI.GetComponentLookup<TrainCar>(),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(),
                DeltaTime = SystemAPI.Time.DeltaTime,
                Paused = paused,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new TrainCarJob {
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                PointLookup = SystemAPI.GetBufferLookup<Point>(true),
                TrainLookup = SystemAPI.GetComponentLookup<Train>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct TrainJob : IJobEntity {
            [ReadOnly]
            public ComponentLookup<Node> NodeLookup;

            [ReadOnly]
            public ComponentLookup<Coaster> CoasterLookup;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<TrainCar> TrainCarLookup;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<LocalTransform> LocalTransformLookup;

            [ReadOnly]
            public float DeltaTime;

            [ReadOnly]
            public bool Paused;

            public void Execute(Entity entity, in CoasterReference coasterEntity, ref Train train) {
                if (!LocalTransformLookup.TryGetComponent(entity, out var transform)) return;

                if (!train.Enabled ||
                    !CoasterLookup.TryGetComponent(coasterEntity.Value, out var coaster) ||
                    coaster.RootNode == Entity.Null) {
                    transform.Position = new float3(0f, -999f, 0f);
                    LocalTransformLookup[entity] = transform;
                    return;
                }

                if (train.Section == Entity.Null ||
                    !PointLookup.TryGetBuffer(train.Section, out var points) ||
                    points.Length < 2) {
                    Reset(coaster, ref train);
                    return;
                }

                if (!train.Kinematic && !Paused) {
                    train.Position += DeltaTime * HZ;
                    if (train.Position > points.Length - 1) {
                        float overshoot = train.Position - (points.Length - 1);

                        if (NodeLookup.TryGetComponent(train.Section, out var node) && node.Next != Entity.Null) {
                            train.Section = node.Next;
                            train.Position = overshoot;
                            points = PointLookup[train.Section];
                        }
                        else {
                            train.Position = points.Length - 1;
                        }
                    }
                }

                train.Position = math.clamp(train.Position, 0f, points.Length - 1f);
                int index = (int)math.floor(train.Position);
                float t = train.Position - index;

                if (index >= points.Length - 1) {
                    index = points.Length - 2;
                    t = 1f;
                }

                float3 position = GetPosition(points, index, t);
                quaternion rotation = GetRotation(points, index, t);

                transform.Position = position;
                transform.Rotation = rotation;
                LocalTransformLookup[entity] = transform;
            }

            private void Reset(in Coaster coaster, ref Train train) {
                train.Section = coaster.RootNode;
                train.Position = 1f;
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

        [BurstCompile]
        private partial struct TrainCarJob : IJobEntity {
            [ReadOnly]
            public ComponentLookup<Node> NodeLookup;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            [ReadOnly]
            public ComponentLookup<Train> TrainLookup;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<LocalTransform> LocalTransformLookup;

            public void Execute(Entity entity, ref TrainCar car) {
                if (!TrainLookup.TryGetComponent(car.Train, out var train)) {
                    return;
                }

                float targetDistance = car.CarIndex * (car.Length + car.CouplerOffset);
                float currentPosition = train.Position;
                Entity currentSection = train.Section;

                if (!PointLookup.TryGetBuffer(currentSection, out var points)) {
                    return;
                }

                int facing = points.Length > 0 ? points[0].Value.Facing : 1;
                float distanceTraveled = 0f;

                while (distanceTraveled < targetDistance) {
                    if (facing > 0) {
                        if (currentPosition <= 0) {
                            if (NodeLookup.TryGetComponent(currentSection, out var node) && node.Previous != Entity.Null) {
                                currentSection = node.Previous;
                                points = PointLookup[currentSection];
                                currentPosition = points.Length - 1;
                            }
                            else {
                                break;
                            }
                        }

                        float segmentLength = math.min(currentPosition, targetDistance - distanceTraveled);
                        currentPosition -= segmentLength / HZ;
                        distanceTraveled += segmentLength;
                    }
                    else {
                        if (currentPosition >= points.Length - 1) {
                            if (NodeLookup.TryGetComponent(currentSection, out var node) && node.Next != Entity.Null) {
                                currentSection = node.Next;
                                points = PointLookup[currentSection];
                                currentPosition = 0;
                            }
                            else {
                                break;
                            }
                        }

                        float segmentLength = math.min(points.Length - 1 - currentPosition, targetDistance - distanceTraveled);
                        currentPosition += segmentLength / HZ;
                        distanceTraveled += segmentLength;
                    }
                }

                car.Section = currentSection;
                car.Position = math.clamp(currentPosition, 0f, points.Length - 1f);

                int index = (int)math.floor(car.Position);
                float t = car.Position - index;

                if (index >= points.Length - 1) {
                    index = points.Length - 2;
                    t = 1f;
                }

                ref var transform = ref LocalTransformLookup.GetRefRW(entity).ValueRW;
                transform.Position = GetPosition(points, index, t);
                transform.Rotation = GetRotation(points, index, t, facing);
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

            private quaternion GetRotation(DynamicBuffer<Point> points, int index, float t, int facing) {
                PointData point = points[index].Value;
                PointData next = points[index + 1].Value;
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
