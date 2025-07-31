using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class ReloadTrackStyleSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<ReloadTrackStyleEvent>();
        }

        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<ReloadTrackStyleEvent>().WithEntityAccess()) {
                foreach (var (_, coaster) in SystemAPI.Query<Coaster>().WithAll<EditorCoasterTag>().WithEntityAccess()) {
                    var loadEntity = ecb.CreateEntity();
                    ecb.AddComponent(loadEntity, new LoadTrackStyleConfigEvent {
                        Target = coaster,
                        ConfigFilename = Preferences.CurrentTrackStyle
                    });
                    ecb.SetName(loadEntity, "Reload Track Style Config Event");
                }
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }
    }
}
