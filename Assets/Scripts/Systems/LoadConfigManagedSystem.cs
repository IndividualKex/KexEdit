using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class LoadConfigManagedSystem : SystemBase {
        private bool _loaded = false;

        protected override void OnCreate() {
            RequireForUpdate<TrackMeshConfig>();
        }

        protected override void OnUpdate() {
            if (_loaded) return;

            var configEntity = SystemAPI.ManagedAPI.GetSingletonEntity<TrackMeshConfig>();
            var managedConfig = SystemAPI.ManagedAPI.GetComponent<TrackMeshConfig>(configEntity);

            var config = TrackMeshResourceLoader.LoadConfig();

            var duplicationMeshes = TrackMeshResourceLoader.LoadDuplicationMeshes(
                config.DuplicationMeshes,
                managedConfig.DuplicationMaterial
            );

            var extrusionMeshes = TrackMeshResourceLoader.LoadExtrusionMeshes(
                config.ExtrusionMeshes,
                managedConfig.ExtrusionMaterial
            );

            var duplicationGizmos = TrackMeshResourceLoader.LoadDuplicationGizmos(
                config.DuplicationGizmos,
                managedConfig.DuplicationGizmoMaterial
            );

            var extrusionGizmos = TrackMeshResourceLoader.LoadExtrusionGizmos(
                config.ExtrusionGizmos,
                managedConfig.ExtrusionGizmoMaterial
            );

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.AddComponent(configEntity, new TrackMeshSettings {
                Spacing = config.Spacing
            });
            ecb.AddComponent(configEntity, new TrackMeshConfigManaged {
                DuplicationMeshes = duplicationMeshes,
                ExtrusionMeshes = extrusionMeshes,
                DuplicationGizmos = duplicationGizmos,
                ExtrusionGizmos = extrusionGizmos
            });
            ecb.Playback(EntityManager);

            _loaded = true;
        }
    }
}
