using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [UpdateAfter(typeof(TrackColliderCleanupSystem))]
    public partial class TrackSegmentCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (coaster, section, segment, trackStyle, entity) in SystemAPI
                .Query<CoasterReference, SectionReference, Segment, TrackStyle>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasBuffer<Point>(section) ||
                    !SystemAPI.HasComponent<StyleHash>(section) ||
                    !SystemAPI.HasComponent<TrackStyleReference>(coaster)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                Entity styleEntity = SystemAPI.GetComponent<TrackStyleReference>(coaster);
                if (!SystemAPI.ManagedAPI.HasComponent<TrackStyleSettings>(styleEntity)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var settings = SystemAPI.ManagedAPI.GetComponent<TrackStyleSettings>(styleEntity);
                if (trackStyle.Version != settings.Version) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var sectionRenderVersion = SystemAPI.GetComponent<RenderedStyleHash>(section);
                var sectionStyleHash = SystemAPI.GetComponent<StyleHash>(section);

                if (segment.StyleHash != sectionStyleHash.Value && segment.StyleHash != sectionRenderVersion.Value) {
                    ecb.DestroyEntity(entity);
                }
            }
            ecb.Playback(EntityManager);
        }
    }
}
