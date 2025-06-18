using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ColliderCleanupSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (nodeReference, entity) in SystemAPI.Query<NodeReference>().WithEntityAccess()) {
                if (SystemAPI.HasBuffer<TrackPoint>(nodeReference.Value)) continue;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
