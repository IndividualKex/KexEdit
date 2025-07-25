using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class TrackStyleInitializationSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI
                .Query<GlobalSettings>()
                .WithNone<TrackStyleSettings>()
                .WithEntityAccess()
            ) {
                ecb.AddComponent(entity, new TrackStyleSettings());
            }

            ecb.Playback(EntityManager);
        }
    }
}
