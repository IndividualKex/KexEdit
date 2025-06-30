using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class TrackMeshDataCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (trackMeshData, entity) in SystemAPI.Query<TrackMeshData>().WithEntityAccess()) {
                if (!EntityManager.Exists(trackMeshData.Entity)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                if (!SystemAPI.GetComponent<Render>(trackMeshData.Entity).Value) {
                    ecb.RemoveComponent<HasTrackMeshDataTag>(trackMeshData.Entity);
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
