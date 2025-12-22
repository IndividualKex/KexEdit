using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct TrackColliderCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (node, segment, entity) in SystemAPI
                .Query<NodeReference, SegmentReference>()
                .WithEntityAccess()
            ) {
                bool sectionExists = SystemAPI.HasBuffer<CorePointBuffer>(node);
                bool segmentExists = SystemAPI.HasComponent<Segment>(segment);
                if (sectionExists && segmentExists) continue;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
