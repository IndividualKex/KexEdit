using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class LoadTrackStyleSettingsSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<TrackStyleSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var settings = SystemAPI.ManagedAPI.GetSingleton<TrackStyleSettings>();

            if (settings.Version == 0) {
                LoadPalette(settings, globalSettings);
                return;
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadTrackStyleConfigEvent>().WithEntityAccess()) {
                Dispose(settings);
                LoadPalette(settings, globalSettings);
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }

        private void LoadPalette(
            TrackStyleSettings settings,
            GlobalSettings globalSettings
        ) {
            var config = TrackStyleResourceLoader.LoadConfig(Preferences.CurrentTrackStyle);

            settings.Styles.Clear();
            settings.DefaultStyle = config.DefaultStyle;
            settings.AutoStyle = Preferences.AutoStyle;

            for (int i = 0; i < config.Styles.Count; i++) {
                var styleConfig = config.Styles[i];

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

                var trackStyle = new TrackStyle {
                    Spacing = styleConfig.Spacing,
                    Threshold = styleConfig.Threshold,
                    DuplicationMeshes = duplicationMeshes,
                    ExtrusionMeshes = extrusionMeshes,
                    StartCapMeshes = startCapMeshes,
                    EndCapMeshes = endCapMeshes
                };

                settings.Styles.Add(trackStyle);
            }

            settings.Version++;
        }

        private void Dispose(TrackStyleSettings settings) {
            foreach (var style in settings.Styles) {
                foreach (var mesh in style.DuplicationMeshes) {
                    if (mesh.Material != null) {
                        UnityEngine.Object.DestroyImmediate(mesh.Material);
                    }
                }

                foreach (var mesh in style.ExtrusionMeshes) {
                    if (mesh.Material != null) {
                        UnityEngine.Object.DestroyImmediate(mesh.Material);
                    }
                }

                foreach (var mesh in style.StartCapMeshes) {
                    if (mesh.Material != null) {
                        UnityEngine.Object.DestroyImmediate(mesh.Material);
                    }
                }

                foreach (var mesh in style.EndCapMeshes) {
                    if (mesh.Material != null) {
                        UnityEngine.Object.DestroyImmediate(mesh.Material);
                    }
                }
            }
        }
    }
}
