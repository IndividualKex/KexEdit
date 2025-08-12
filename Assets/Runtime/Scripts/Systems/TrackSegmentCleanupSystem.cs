using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [UpdateAfter(typeof(TrackColliderCleanupSystem))]
    [BurstCompile]
    public partial struct TrackSegmentCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (coaster, section, segment, entity) in SystemAPI
                .Query<CoasterReference, SectionReference, Segment>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasComponent<TrackStyle>(segment.Style) ||
                    !SystemAPI.HasComponent<StyleHash>(section) ||
                    !SystemAPI.HasComponent<TrackStyleSettingsReference>(coaster)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var settingsEntity = SystemAPI.GetComponent<TrackStyleSettingsReference>(coaster);
                if (!SystemAPI.HasComponent<TrackStyleSettings>(settingsEntity)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var sectionRenderVersion = SystemAPI.GetComponent<RenderedStyleHash>(section);
                var sectionStyleHash = SystemAPI.GetComponent<StyleHash>(section);

                if (segment.StyleHash != sectionStyleHash.Value && segment.StyleHash != sectionRenderVersion.Value) {
                    ecb.DestroyEntity(entity);
                }
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
