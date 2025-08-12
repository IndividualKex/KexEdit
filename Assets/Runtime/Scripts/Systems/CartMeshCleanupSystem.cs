using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct CartMeshCleanupSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (cartMesh, entity) in SystemAPI.Query<CartMesh>().WithEntityAccess()) {
                if (SystemAPI.HasComponent<CartMeshReference>(cartMesh.Cart) &&
                    SystemAPI.GetComponent<CartMeshReference>(cartMesh.Cart).Value == entity) continue;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
