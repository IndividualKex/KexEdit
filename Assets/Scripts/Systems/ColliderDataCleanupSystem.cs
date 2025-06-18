using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class ColliderDataCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (hasColliderDataTag, entity) in SystemAPI.Query<HasColliderDataTag>().WithEntityAccess()) {
                if (!EntityManager.Exists(entity)) continue;

                if (!SystemAPI.GetComponent<Render>(entity).Value) {
                    var colliderReferenceBuffer = SystemAPI.GetBuffer<ColliderReference>(entity);
                    for (int i = 0; i < colliderReferenceBuffer.Length; i++) {
                        ecb.DestroyEntity(colliderReferenceBuffer[i]);
                    }

                    ecb.RemoveComponent<ColliderHash>(entity);
                    ecb.RemoveComponent<ColliderReference>(entity);
                    ecb.RemoveComponent<HasColliderDataTag>(entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
