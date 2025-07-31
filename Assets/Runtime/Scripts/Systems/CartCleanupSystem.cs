using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct CartCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (coaster, entity) in SystemAPI
                .Query<CoasterReference>()
                .WithAll<Cart>()
                .WithEntityAccess()
            ) {
                if (SystemAPI.HasComponent<Coaster>(coaster)) continue;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
