using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct TrackStyleCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (style, entity) in SystemAPI.Query<TrackStyle>().WithEntityAccess()) {
                if (SystemAPI.HasComponent<TrackStyleSettings>(style.Settings)) continue;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
