using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace KexEdit.Legacy {
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
                    !SystemAPI.HasComponent<TrackStyleHash>(section) ||
                    !SystemAPI.HasComponent<TrackStyleSettingsReference>(coaster)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var settingsEntity = SystemAPI.GetComponent<TrackStyleSettingsReference>(coaster);
                if (!SystemAPI.HasComponent<TrackStyleSettings>(settingsEntity)) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var settings = SystemAPI.GetComponent<TrackStyleSettings>(settingsEntity);
                if (segment.StyleSettingsVersion != settings.Version) {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                if (segment.StyleHash != SystemAPI.GetComponent<TrackStyleHash>(section)) {
                    ecb.DestroyEntity(entity);
                }
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
