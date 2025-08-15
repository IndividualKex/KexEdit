using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrainUpdateSystem))]
    [UpdateBefore(typeof(DistanceFollowerUpdateSystem))]
    [BurstCompile]
    public partial struct WheelAssemblyUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            state.Dependency = new Job {
                TrainLookup = SystemAPI.GetComponentLookup<Train>(true),
                TrainCarLookup = SystemAPI.GetComponentLookup<TrainCar>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            [ReadOnly]
            public ComponentLookup<Train> TrainLookup;
            [ReadOnly]
            public ComponentLookup<TrainCar> TrainCarLookup;

            public void Execute(in WheelAssembly wheelAssembly, ref DistanceFollower follower) {
                if (!TrainCarLookup.TryGetComponent(wheelAssembly.TrainCar, out var trainCar) ||
                    !TrainLookup.TryGetComponent(trainCar.Train, out var train) ||
                    !train.Enabled) {
                    follower.Active = false;
                    return;
                }

                follower.Active = true;
                follower.Distance = train.Distance + (wheelAssembly.Offset * train.Facing);
            }
        }
    }
}
