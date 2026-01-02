using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class StyleLoadingSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<LoadTrackStyleSettingsEvent>();
        }

        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadTrackStyleSettingsEvent>().WithEntityAccess()) {
                ecb.AddBuffer<TrackStyleReference>(entity);
                for (int i = 0; i < evt.Data.Styles.Count; i++) {
                    var styleData = evt.Data.Styles[i];
                    var styleEntity = EntityManager.CreateEntity();
                    int stepLCM = 1;
                    foreach (var duplicationMesh in styleData.DuplicationMeshes) {
                        stepLCM = Extensions.LCM(stepLCM, duplicationMesh.Step);
                    }
                    ecb.AddComponent(styleEntity, new TrackStyle {
                        Settings = entity,
                        Spacing = styleData.Spacing,
                        Threshold = styleData.Threshold,
                        Step = stepLCM
                    });
                    ecb.SetName(styleEntity, $"Track Style {i}");

                    ecb.AppendToBuffer<TrackStyleReference>(entity, styleEntity);
                }
                ecb.AddComponent(entity, new TrackStyleSettings {
                    DefaultStyle = evt.Data.DefaultStyle,
                    Version = evt.Data.Version,
                    AutoStyle = evt.Data.AutoStyle,
                });

                ecb.RemoveComponent<LoadTrackStyleSettingsEvent>(entity);
            }
            ecb.Playback(EntityManager);
        }
    }
}
