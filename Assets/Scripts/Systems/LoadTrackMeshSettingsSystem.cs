using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class LoadTrackMeshSettingsSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<TrackMeshGlobalSettings>();
            RequireForUpdate<TrackMeshSettings>();
            RequireForUpdate<TrackMeshSettingsManaged>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<TrackMeshGlobalSettings>();
            ref var settings = ref SystemAPI.GetSingletonRW<TrackMeshSettings>().ValueRW;
            var settingsManaged = SystemAPI.ManagedAPI.GetSingleton<TrackMeshSettingsManaged>();

            if (settings.Version == 0) {
                Load(ref settings, settingsManaged, globalSettings, TrackMeshPreferences.CurrentTrackMesh);
                return;
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadTrackMeshConfigEvent>().WithEntityAccess()) {
                Dispose(settingsManaged);
                Load(ref settings, settingsManaged, globalSettings, evt.ConfigPath);
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }

        private void Load(
            ref TrackMeshSettings settings,
            TrackMeshSettingsManaged settingsManaged,
            TrackMeshGlobalSettings globalSettings,
            string configPath
        ) {
            var config = TrackMeshResourceLoader.LoadConfig(configPath);

            var duplicationMeshes = TrackMeshResourceLoader.LoadDuplicationMeshes(
                config.DuplicationMeshes,
                globalSettings.DuplicationMaterial
            );

            var extrusionMeshes = TrackMeshResourceLoader.LoadExtrusionMeshes(
                config.ExtrusionMeshes,
                globalSettings.ExtrusionMaterial
            );

            settings.Spacing = config.Spacing;
            settings.Version++;

            TrackMeshPreferences.CurrentTrackMesh = configPath;
            settingsManaged.DuplicationMeshes = duplicationMeshes;
            settingsManaged.ExtrusionMeshes = extrusionMeshes;
        }

        private void Dispose(TrackMeshSettingsManaged settings) {
            foreach (var mesh in settings.DuplicationMeshes) {
                if (mesh.Material != null) {
                    UnityEngine.Object.DestroyImmediate(mesh.Material);
                }
            }

            foreach (var mesh in settings.ExtrusionMeshes) {
                if (mesh.Material != null) {
                    UnityEngine.Object.DestroyImmediate(mesh.Material);
                }
            }

        }
    }
}
