using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class MeshDataCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (meshData, entity) in SystemAPI.Query<MeshData>().WithEntityAccess()) {
                if (!EntityManager.Exists(meshData.Entity)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                if (!SystemAPI.GetComponent<Render>(meshData.Entity).Value) {
                    ecb.RemoveComponent<HasMeshDataTag>(meshData.Entity);
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
