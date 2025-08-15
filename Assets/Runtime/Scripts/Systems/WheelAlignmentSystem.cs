using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrainSystem))]
    [BurstCompile]
    public partial struct WheelAlignmentSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            state.Dependency = new Job {
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                PointLookup = SystemAPI.GetBufferLookup<Point>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public ComponentLookup<Node> NodeLookup;

            [ReadOnly]
            public BufferLookup<Point> PointLookup;

            [ReadOnly]
            public ComponentLookup<LocalTransform> LocalTransformLookup;

            public void Execute(Entity entity, in TrainCar car, ref DynamicBuffer<WheelAssembly> wheels) {
                if (car.Section == Entity.Null || !PointLookup.TryGetBuffer(car.Section, out var points)) {
                    return;
                }

                for (int i = 0; i < wheels.Length; i++) {
                    var wheel = wheels[i];

                    float wheelPosition = car.Position;
                    Entity wheelSection = car.Section;
                    var wheelPoints = points;

                    float offset = wheel.Offset;
                    int facing = points.Length > 0 ? points[0].Value.Facing : 1;

                    if (offset != 0) {
                        float distance = math.abs(offset);
                        float direction = math.sign(offset) * facing;

                        if (direction > 0) {
                            while (distance > 0 && wheelPosition < wheelPoints.Length - 1) {
                                float maxMove = (wheelPoints.Length - 1 - wheelPosition) * (1f / HZ);
                                if (maxMove >= distance) {
                                    wheelPosition += distance * HZ;
                                    distance = 0;
                                }
                                else {
                                    distance -= maxMove;
                                    if (NodeLookup.TryGetComponent(wheelSection, out var node) && node.Next != Entity.Null) {
                                        wheelSection = node.Next;
                                        wheelPoints = PointLookup[wheelSection];
                                        wheelPosition = 0;
                                    }
                                    else {
                                        wheelPosition = wheelPoints.Length - 1;
                                        break;
                                    }
                                }
                            }
                        }
                        else {
                            while (distance > 0 && wheelPosition > 0) {
                                float maxMove = wheelPosition * (1f / HZ);
                                if (maxMove >= distance) {
                                    wheelPosition -= distance * HZ;
                                    distance = 0;
                                }
                                else {
                                    distance -= maxMove;
                                    if (NodeLookup.TryGetComponent(wheelSection, out var node) && node.Previous != Entity.Null) {
                                        wheelSection = node.Previous;
                                        wheelPoints = PointLookup[wheelSection];
                                        wheelPosition = wheelPoints.Length - 1;
                                    }
                                    else {
                                        wheelPosition = 0;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    wheel.Section = wheelSection;
                    wheel.Position = math.clamp(wheelPosition, 0f, wheelPoints.Length - 1f);

                    int index = (int)math.floor(wheel.Position);
                    float t = wheel.Position - index;

                    if (index >= wheelPoints.Length - 1) {
                        index = wheelPoints.Length - 2;
                        t = 1f;
                    }

                    wheel.WorldPosition = GetPosition(wheelPoints, index, t);
                    wheel.WorldRotation = GetRotation(wheelPoints, index, t, facing);

                    if (wheel.TrackGauge != 0) {
                        float3 lateral = math.mul(wheel.WorldRotation, math.right());
                        wheel.WorldPosition += 0.5f * wheel.TrackGauge * lateral;
                    }

                    wheels[i] = wheel;
                }
            }

            private float3 GetPosition(DynamicBuffer<Point> points, int index, float t) {
                PointData point = points[index].Value;
                PointData next = points[index + 1].Value;
                return math.lerp(
                    point.Position,
                    next.Position,
                    t
                );
            }

            private quaternion GetRotation(DynamicBuffer<Point> points, int index, float t, int facing) {
                PointData point = points[index].Value;
                PointData next = points[index + 1].Value;
                float3 direction = math.normalize(math.lerp(
                    point.Direction,
                    next.Direction,
                    t
                ));
                float3 lateral = math.normalize(math.lerp(
                    point.Lateral,
                    next.Lateral,
                    t
                ));
                float3 normal = math.normalize(math.cross(direction, lateral));

                float3 finalDirection = facing > 0 ? -direction : direction;
                return quaternion.LookRotation(finalDirection, -normal);
            }
        }
    }
}
