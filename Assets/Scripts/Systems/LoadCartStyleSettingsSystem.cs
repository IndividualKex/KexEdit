using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class LoadCartStyleSettingsSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<CartStyleGlobalSettings>();
            RequireForUpdate<CartStyleSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<CartStyleGlobalSettings>();
            var settings = SystemAPI.ManagedAPI.GetSingleton<CartStyleSettings>();

            if (settings.Version == 0) {
                LoadPalette(settings, globalSettings);
                return;
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadCartStyleConfigEvent>().WithEntityAccess()) {
                Dispose(settings);
                LoadPalette(settings, globalSettings);
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }

        private void LoadPalette(
            CartStyleSettings settings,
            CartStyleGlobalSettings globalSettings
        ) {
            var config = CartStyleResourceLoader.LoadConfig(CartStylePreferences.CurrentCartStyle);

            settings.Styles.Clear();

            for (int i = 0; i < config.Styles.Count; i++) {
                var styleConfig = config.Styles[i];

                var cartStyle = new CartStyle {
                    Mesh = null,
                    MeshPath = styleConfig.MeshPath,
                    Loaded = false,
                };

                settings.Styles.Add(cartStyle);
            }

            settings.Version++;
        }

        private void Dispose(CartStyleSettings settings) {
            foreach (var style in settings.Styles) {
                if (style.Mesh == null) continue;
                UnityEngine.Object.Destroy(style.Mesh);
                style.Mesh = null;
            }
        }
    }
}
