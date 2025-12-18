using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrainUpdateSystem))]
    [UpdateBefore(typeof(TrackFollowerUpdateSystem))]
    [BurstCompile]
    public partial struct TrainCarUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var query = SystemAPI.QueryBuilder().WithAll<TrainCar, TrackFollower>().Build();
            state.Dependency = new Job {
                TrackFollowerLookup = SystemAPI.GetComponentLookup<TrackFollower>(false),
            }.ScheduleParallel(query, state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [NativeDisableParallelForRestriction]
            public ComponentLookup<TrackFollower> TrackFollowerLookup;

            public void Execute(Entity entity, in TrainCar trainCar) {
                if (!TrackFollowerLookup.TryGetComponent(trainCar.Train, out var trainFollower)) {
                    return;
                }

                ref var followerRef = ref TrackFollowerLookup.GetRefRW(entity).ValueRW;
                followerRef = trainFollower;
            }
        }
    }
}
