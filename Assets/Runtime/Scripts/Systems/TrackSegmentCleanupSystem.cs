using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ColliderDataCleanupSystem))]
    [UpdateBefore(typeof(TrackSegmentInitializationSystem))]
    public partial class TrackSegmentCleanupSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<TrackStyleSettings>();
        }

        protected override void OnUpdate() {
            var settings = SystemAPI.ManagedAPI.GetSingleton<TrackStyleSettings>();
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (section, segment, trackStyle, entity) in SystemAPI
                .Query<SectionReference, Segment, TrackStyle>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasBuffer<Point>(section) ||
                    !SystemAPI.HasComponent<StyleHash>(section) ||
                    trackStyle.Version != settings.Version) {
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
