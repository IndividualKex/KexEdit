using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class LoadTrackStyleConfigSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<LoadTrackStyleConfigEvent>();
        }

        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadTrackStyleConfigEvent>().WithEntityAccess()) {
                if (SystemAPI.HasComponent<TrackStyleReference>(evt.Target)) {
                    Entity styleEntity = SystemAPI.GetComponent<TrackStyleReference>(evt.Target);
                    Dispose(styleEntity);
                    ecb.DestroyEntity(styleEntity);
                    ecb.RemoveComponent<TrackStyleReference>(evt.Target);
                }

                Preferences.CurrentTrackStyle = evt.ConfigFilename;
                var version = (int)(System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond);

                var loadEntity = ecb.CreateEntity();
                var config = TrackStyleResourceLoader.LoadConfig(evt.ConfigFilename);
                var trackStyleData = ConvertConfigToData(config, version);
                ecb.AddComponent(loadEntity, new LoadTrackStyleEvent {
                    Target = evt.Target,
                    TrackStyle = trackStyleData
                });
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private TrackStyleData ConvertConfigToData(TrackStyleConfig config, int version) {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var styles = new List<TrackStyleMeshData>();

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

                styles.Add(new TrackStyleMeshData {
                    DuplicationMeshes = duplicationMeshes,
                    ExtrusionMeshes = extrusionMeshes,
                    StartCapMeshes = startCapMeshes,
                    EndCapMeshes = endCapMeshes,
                    Spacing = styleConfig.Spacing,
                    Threshold = styleConfig.Threshold
                });
            }

            return new TrackStyleData {
                Styles = styles,
                DefaultStyle = config.DefaultStyle,
                AutoStyle = Preferences.AutoStyle,
                Version = version
            };
        }

        private void Dispose(Entity entity) {
            if (!SystemAPI.ManagedAPI.HasComponent<TrackStyleSettings>(entity)) return;
            var settings = SystemAPI.ManagedAPI.GetComponent<TrackStyleSettings>(entity);

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
