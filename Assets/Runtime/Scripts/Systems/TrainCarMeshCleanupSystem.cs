using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct TrainCarMeshCleanupSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (mesh, entity) in SystemAPI.Query<TrainCarMesh>().WithEntityAccess()) {
                if (SystemAPI.HasComponent<TrainCarMeshReference>(mesh.TrainCar) &&
                    SystemAPI.GetComponent<TrainCarMeshReference>(mesh.TrainCar).Value == entity) continue;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
