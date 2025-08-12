using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CartSystem))]
    [BurstCompile]
    public partial struct CartMeshUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            foreach (var (mesh, transform) in SystemAPI.Query<CartMeshReference, LocalTransform>()) {
                if (mesh.Value == Entity.Null || !SystemAPI.HasComponent<LocalTransform>(mesh.Value)) continue;
                ref var meshTransform = ref SystemAPI.GetComponentRW<LocalTransform>(mesh.Value).ValueRW;
                meshTransform = transform;
            }
        }
    }
}
