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
                if (IsValid(ref state, entity, mesh.TrainCar)) continue;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }

        private bool IsValid(ref SystemState state, Entity meshEntity, Entity trainCarEntity) {
            if (!SystemAPI.HasBuffer<TrainCarMeshReference>(trainCarEntity)) return false;
            var references = SystemAPI.GetBuffer<TrainCarMeshReference>(trainCarEntity);
            for (int i = 0; i < references.Length; i++) {
                if (references[i] == meshEntity) return true;
            }
            return false;
        }
    }
}
