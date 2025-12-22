using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [UpdateAfter(typeof(TrainCarCleanupSystem))]
    [BurstCompile]
    public partial struct WheelAssemblyCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (wheelAssembly, entity) in SystemAPI.Query<WheelAssembly>().WithEntityAccess()) {
                if (!SystemAPI.HasComponent<TrainCar>(wheelAssembly.TrainCar)) {
                    ecb.DestroyEntity(entity);
                }
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
