using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class LoadTrainStyleSettingsSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<TrainStyleSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var settings = SystemAPI.ManagedAPI.GetSingleton<TrainStyleSettings>();

            if (settings.Version == 0) {
                LoadPalette(settings, globalSettings);
                return;
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadTrainStyleConfigEvent>().WithEntityAccess()) {
                Dispose(settings, ecb);
                LoadPalette(settings, globalSettings);
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }

        private void LoadPalette(
            TrainStyleSettings settings,
            GlobalSettings globalSettings
        ) {
            var config = TrainStyleResourceLoader.LoadConfig(Preferences.CurrentTrainSTyle);

            settings.Styles.Clear();

            for (int i = 0; i < config.Styles.Count; i++) {
                var styleConfig = config.Styles[i];

                var cartStyle = new TrainStyle {
                    Mesh = Entity.Null,
                    MeshPath = styleConfig.MeshPath,
                    Loaded = false,
                };

                settings.Styles.Add(cartStyle);
            }

            settings.Version++;
        }

        private void Dispose(TrainStyleSettings settings, EntityCommandBuffer ecb) {
            foreach (var style in settings.Styles) {
                if (style.Mesh == Entity.Null) continue;
                ecb.DestroyEntity(style.Mesh);
                style.Mesh = Entity.Null;
            }
        }
    }
}
