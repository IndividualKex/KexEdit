using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct MeshCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (nodeMesh, entity) in SystemAPI.Query<NodeMesh>().WithEntityAccess()) {
                if (SystemAPI.HasComponent<MeshReference>(nodeMesh.Node) &&
                    SystemAPI.GetComponent<MeshReference>(nodeMesh.Node).Value == entity) continue;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
