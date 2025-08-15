using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class TrainStyleInitializationSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI
                .Query<GlobalSettings>()
                .WithNone<TrainStyleSettings>()
                .WithEntityAccess()
            ) {
                ecb.AddComponent(entity, new TrainStyleSettings());
            }

            foreach (var (coaster, entity) in SystemAPI
                .Query<CoasterReference>()
                .WithAll<TrainCar>()
                .WithNone<TrainStyleReference>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasComponent<EditorCoasterTag>(coaster)) continue;
                ecb.AddComponent(entity, new TrainStyleReference {
                    StyleIndex = 0,
                    Version = 0
                });
            }

            ecb.Playback(EntityManager);
        }
    }
}
