using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct ConnectionCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (connection, entity) in SystemAPI.Query<Connection>().WithEntityAccess()) {
                if (!SystemAPI.HasComponent<Port>(connection.Source) ||
                    !SystemAPI.HasComponent<Port>(connection.Target)) {
                    ecb.DestroyEntity(entity);
                }
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
