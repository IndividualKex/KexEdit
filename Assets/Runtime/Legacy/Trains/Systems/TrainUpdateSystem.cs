using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Constants;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TrackFollowerUpdateSystem))]
    [BurstCompile]
    public partial struct TrainUpdateSystem : ISystem {
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
                DeltaTime = SystemAPI.Time.DeltaTime,
                Paused = paused,
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

            [ReadOnly]
            public float DeltaTime;

            [ReadOnly]
            public bool Paused;

            public void Execute(Entity entity, in CoasterReference coasterEntity, ref Train train, ref TrackFollower follower) {
                if (!train.Enabled ||
                    !CoasterLookup.TryGetComponent(coasterEntity.Value, out var coaster) ||
                    coaster.RootNode == Entity.Null) {
                    follower.Active = false;
                    return;
                }

                follower.Active = true;

                if (follower.Section == Entity.Null ||
                    !PointLookup.TryGetBuffer(follower.Section, out var points) ||
                    points.Length < 2) {
                    Reset(coaster, ref follower);
                    return;
                }

                if (!train.Kinematic && !Paused) {
                    follower.Index += DeltaTime * HZ;
                    if (follower.Index > points.Length - 1) {
                        float overshoot = follower.Index - (points.Length - 1);

                        if (NodeLookup.TryGetComponent(follower.Section, out var node) && node.Next != Entity.Null) {
                            follower.Section = node.Next;
                            follower.Index = overshoot;
                            points = PointLookup[follower.Section];
                        }
                        else {
                            follower.Index = points.Length - 1;
                        }
                    }
                }

                int intIndex = (int)math.floor(follower.Index);
                float t = follower.Index - intIndex;
                if (intIndex >= points.Length - 1) {
                    intIndex = points.Length - 2;
                    t = 1f;
                }
                if (intIndex < 0) {
                    intIndex = 0;
                    t = 0f;
                }

                float distance = GetDistance(points, intIndex, t);
                int facing = points[intIndex].Value.Facing;
                train.Distance = distance;
                train.Facing = facing;
            }

            private float GetDistance(DynamicBuffer<Point> points, int index, float t) {
                PointData point = points[index].Value;
                PointData next = points[index + 1].Value;
                return math.lerp(
                    point.TotalLength,
                    next.TotalLength,
                    t
                );
            }

            private void Reset(in Coaster coaster, ref TrackFollower follower) {
                follower.Section = coaster.RootNode;
                follower.Index = 1f;
            }
        }
    }
}
