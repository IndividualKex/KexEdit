using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrainSystem))]
    [BurstCompile]
    public partial struct TrainCarMeshUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            foreach (var (mesh, transform) in SystemAPI.Query<TrainCarMeshReference, LocalTransform>()) {
                if (mesh.Value == Entity.Null || !SystemAPI.HasComponent<LocalTransform>(mesh.Value)) continue;
                ref var meshTransform = ref SystemAPI.GetComponentRW<LocalTransform>(mesh.Value).ValueRW;
                meshTransform = transform;
            }
        }
    }
}