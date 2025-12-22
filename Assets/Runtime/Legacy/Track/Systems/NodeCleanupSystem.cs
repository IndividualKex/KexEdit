using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(CleanupSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct NodeCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (node, coaster, inputPorts, outputPorts, entity) in SystemAPI
                .Query<Node, CoasterReference, DynamicBuffer<InputPortReference>, DynamicBuffer<OutputPortReference>>()
                .WithEntityAccess()
            ) {
                if (SystemAPI.HasComponent<Coaster>(coaster)) continue;
                foreach (var port in inputPorts) {
                    ecb.DestroyEntity(port);
                }
                foreach (var port in outputPorts) {
                    ecb.DestroyEntity(port);
                }
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
