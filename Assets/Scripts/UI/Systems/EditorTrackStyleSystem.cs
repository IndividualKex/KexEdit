using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class EditorTrackStyleSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<EditorTrackStyleSettingsSingleton>();
        }

        protected override void OnUpdate() {
            UpdateAssignments();
            UpdateSettings();
        }

        private void UpdateAssignments() {
            var singleton = SystemAPI.GetSingleton<EditorTrackStyleSettingsSingleton>();

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            if (SystemAPI.HasComponent<TrackStyleSettings>(singleton.Settings)) {
                foreach (var (_, coaster) in SystemAPI.Query<Coaster>().WithAll<EditorCoasterTag>().WithEntityAccess()) {
                    if (SystemAPI.HasComponent<TrackStyleSettingsReference>(coaster)) {
                        var settings = SystemAPI.GetComponent<TrackStyleSettingsReference>(coaster);
                        if (settings.Value.Equals(singleton.Settings)) continue;
                        var settingsRW = SystemAPI.GetComponentRW<TrackStyleSettingsReference>(coaster);
                        settingsRW.ValueRW.Value = singleton.Settings;
                    }
                    else {
                        ecb.AddComponent<TrackStyleSettingsReference>(coaster, singleton.Settings);
                    }
                }
            }
            ecb.Playback(EntityManager);
        }

        private void UpdateSettings() {
            var singletonRW = SystemAPI.GetSingletonRW<EditorTrackStyleSettingsSingleton>();

            if (!singletonRW.ValueRO.Dirty) return;
            singletonRW.ValueRW.Dirty = false;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            int version = 0;
            if (SystemAPI.HasComponent<TrackStyleSettings>(singletonRW.ValueRO.Settings)) {
                var settings = SystemAPI.GetComponent<TrackStyleSettings>(singletonRW.ValueRO.Settings);
                version = settings.Version + 1;
                ecb.DestroyEntity(singletonRW.ValueRO.Settings);
                singletonRW.ValueRW.Settings = Entity.Null;
            }

            var settingsEntity = EntityManager.CreateEntity();
            singletonRW.ValueRW.Settings = settingsEntity;

            var config = TrackStyleResourceLoader.LoadConfig(Preferences.CurrentTrackStyle);
            var data = ConvertConfigToData(config, version);
            ecb.AddComponent(settingsEntity, new LoadTrackStyleSettingsEvent {
                Data = data
            });
            ecb.SetName(settingsEntity, "Track Style Settings");

            ecb.Playback(EntityManager);
        }

        private TrackStyleSettingsData ConvertConfigToData(TrackStyleConfig config, int version) {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var styles = new List<TrackStyleData>();

            foreach (var styleConfig in config.Styles) {
                var duplicationMeshes = TrackStyleResourceLoader.LoadDuplicationMeshes(
                    styleConfig.DuplicationMeshes,
                    globalSettings.DuplicationMaterial,
                    config
                );

                var extrusionMeshes = TrackStyleResourceLoader.LoadExtrusionMeshes(
                    styleConfig.ExtrusionMeshes,
                    globalSettings.ExtrusionMaterial,
                    config
                );

                var startCapMeshes = TrackStyleResourceLoader.LoadCapMeshes(
                    styleConfig.StartCapMeshes,
                    globalSettings.DuplicationMaterial,
                    config
                );

                var endCapMeshes = TrackStyleResourceLoader.LoadCapMeshes(
                    styleConfig.EndCapMeshes,
                    globalSettings.DuplicationMaterial,
                    config
                );

                styles.Add(new TrackStyleData {
                    DuplicationMeshes = duplicationMeshes,
                    ExtrusionMeshes = extrusionMeshes,
                    StartCapMeshes = startCapMeshes,
                    EndCapMeshes = endCapMeshes,
                    Spacing = styleConfig.Spacing,
                    Threshold = styleConfig.Threshold
                });
            }

            return new TrackStyleSettingsData {
                Styles = styles,
                DefaultStyle = config.DefaultStyle,
                AutoStyle = Preferences.AutoStyle,
                Version = version
            };
        }
    }
}
