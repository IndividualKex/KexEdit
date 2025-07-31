using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class CartStyleInitializationSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI
                .Query<GlobalSettings>()
                .WithNone<CartStyleSettings>()
                .WithEntityAccess()
            ) {
                ecb.AddComponent(entity, new CartStyleSettings());
            }

            foreach (var (coaster, entity) in SystemAPI
                .Query<CoasterReference>()
                .WithAll<Cart>()
                .WithNone<CartStyleReference>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasComponent<EditorCoasterTag>(coaster)) continue;
                ecb.AddComponent(entity, new CartStyleReference {
                    StyleIndex = 0,
                    Version = 0
                });
            }

            ecb.Playback(EntityManager);
        }
    }
}
