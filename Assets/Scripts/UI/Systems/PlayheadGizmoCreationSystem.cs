using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PlayheadGizmoCreationSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (coaster, entity) in SystemAPI
                .Query<Coaster>()
                .WithAll<EditorCoasterTag>()
                .WithNone<PlayheadGizmoReference>()
                .WithEntityAccess()
            ) {
                var playheadGizmoEntity = ecb.CreateEntity();
                ecb.AddComponent(playheadGizmoEntity, LocalTransform.Identity);
                ecb.AddComponent<PlayheadGizmoReference>(entity, playheadGizmoEntity);
                ecb.AddComponent<CoasterReference>(playheadGizmoEntity, entity);
                ecb.AddComponent(playheadGizmoEntity, new Cart {
                    Position = 1f,
                    Enabled = true,
                    Kinematic = true
                });
                ecb.AddComponent(playheadGizmoEntity, new CartStyleReference {
                    StyleIndex = 0,
                    Version = 0
                });
                ecb.AddComponent(playheadGizmoEntity, new CartMeshReference());
                ecb.AddComponent(playheadGizmoEntity, new RenderTag {
                    Type = RenderTagType.Playhead
                });
                ecb.SetName(playheadGizmoEntity, "Playhead Gizmo");
            }
            ecb.Playback(EntityManager);
        }
    }
}
