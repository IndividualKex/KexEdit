using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct NodeCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (node, entity) in SystemAPI
                .Query<NodeAspect>()
                .WithAll<Node>()
                .WithEntityAccess()
            ) {
                if (SystemAPI.HasComponent<Coaster>(node.Coaster)) continue;
                foreach (var port in node.InputPorts) {
                    ecb.DestroyEntity(port);
                }
                foreach (var port in node.OutputPorts) {
                    ecb.DestroyEntity(port);
                }
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
