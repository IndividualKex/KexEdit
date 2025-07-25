using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TrackStyleLoadingSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<LoadTrackStyleEvent>();
        }

        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadTrackStyleEvent>().WithEntityAccess()) {
                var settingsEntity = ecb.CreateEntity();
                var styles = new List<TrackStyle>();
                for (int i = 0; i < evt.TrackStyle.Styles.Count; i++) {
                    var styleData = evt.TrackStyle.Styles[i];
                    var extrusionMeshes = new List<ExtrusionMeshSettings>();
                    foreach (var extrusionMesh in styleData.ExtrusionMeshes) {
                        if (!ExtrusionMeshConverter.Convert(extrusionMesh.Mesh, out var outputMesh)) {
                            UnityEngine.Debug.LogError("Failed to convert extrusion mesh");
                            continue;
                        }
                        extrusionMeshes.Add(new ExtrusionMeshSettings {
                            Mesh = outputMesh,
                            Material = extrusionMesh.Material,
                        });
                    }
                    var style = new TrackStyle {
                        DuplicationMeshes = styleData.DuplicationMeshes,
                        ExtrusionMeshes = extrusionMeshes,
                        StartCapMeshes = styleData.StartCapMeshes,
                        EndCapMeshes = styleData.EndCapMeshes,
                        Spacing = styleData.Spacing,
                        Threshold = styleData.Threshold,
                    };
                    styles.Add(style);
                }
                ecb.AddComponent(settingsEntity, new TrackStyleSettings {
                    Styles = styles,
                    DefaultStyle = evt.TrackStyle.DefaultStyle,
                    AutoStyle = evt.TrackStyle.AutoStyle,
                });
                ecb.SetName(settingsEntity, "Track Style Settings");
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }
    }
}
