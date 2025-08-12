using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TrackStyleSettingsLoadingSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<LoadTrackStyleSettingsEvent>();
        }

        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadTrackStyleSettingsEvent>().WithEntityAccess()) {
                ecb.AddBuffer<TrackStyleReference>(entity);
                for (int i = 0; i < evt.Data.Styles.Count; i++) {
                    var styleData = evt.Data.Styles[i];
                    var extrusionMeshes = new List<ExtrusionMeshSettingsData>();
                    foreach (var extrusionMesh in styleData.ExtrusionMeshes) {
                        if (!ExtrusionMeshConverter.Convert(extrusionMesh.Mesh, out var outputMesh)) {
                            UnityEngine.Debug.LogError("Failed to convert extrusion mesh");
                            continue;
                        }
                        extrusionMeshes.Add(new ExtrusionMeshSettingsData {
                            Mesh = outputMesh,
                            Material = extrusionMesh.Material,
                        });
                    }
                    var styleEntity = EntityManager.CreateEntity();

                    ecb.AddBuffer<DuplicationMeshReference>(styleEntity);
                    ecb.AddBuffer<ExtrusionMeshReference>(styleEntity);
                    ecb.AddBuffer<StartCapMeshReference>(styleEntity);
                    ecb.AddBuffer<EndCapMeshReference>(styleEntity);
                    foreach (var duplicationMeshData in styleData.DuplicationMeshes) {
                        var duplicationMeshEntity = EntityManager.CreateEntity();
                        ecb.AddComponent(duplicationMeshEntity, new DuplicationMeshSettings {
                            Step = duplicationMeshData.Step,
                            Offset = duplicationMeshData.Offset,
                        });
                        ecb.AddComponent<MeshReference>(duplicationMeshEntity, duplicationMeshData.Mesh);
                        ecb.AddComponent<MaterialReference>(duplicationMeshEntity, duplicationMeshData.Material);
                        ecb.AppendToBuffer<DuplicationMeshReference>(styleEntity, duplicationMeshEntity);
                        ecb.SetName(duplicationMeshEntity, $"Duplication Mesh {i}");
                    }
                    foreach (var extrusionMeshData in extrusionMeshes) {
                        var extrusionMeshEntity = EntityManager.CreateEntity();
                        ecb.AddComponent<MeshReference>(extrusionMeshEntity, extrusionMeshData.Mesh);
                        ecb.AddComponent<MaterialReference>(extrusionMeshEntity, extrusionMeshData.Material);
                        ecb.AppendToBuffer<ExtrusionMeshReference>(styleEntity, extrusionMeshEntity);
                        ecb.SetName(extrusionMeshEntity, $"Extrusion Mesh {i}");
                    }
                    foreach (var startCapMeshData in styleData.StartCapMeshes) {
                        var startCapMeshEntity = EntityManager.CreateEntity();
                        ecb.AddComponent<MeshReference>(startCapMeshEntity, startCapMeshData.Mesh);
                        ecb.AddComponent<MaterialReference>(startCapMeshEntity, startCapMeshData.Material);
                        ecb.AppendToBuffer<StartCapMeshReference>(styleEntity, startCapMeshEntity);
                        ecb.SetName(startCapMeshEntity, $"Start Cap Mesh {i}");
                    }
                    foreach (var endCapMeshData in styleData.EndCapMeshes) {
                        var endCapMeshEntity = EntityManager.CreateEntity();
                        ecb.AddComponent<MeshReference>(endCapMeshEntity, endCapMeshData.Mesh);
                        ecb.AddComponent<MaterialReference>(endCapMeshEntity, endCapMeshData.Material);
                        ecb.AppendToBuffer<EndCapMeshReference>(styleEntity, endCapMeshEntity);
                        ecb.SetName(endCapMeshEntity, $"End Cap Mesh {i}");
                    }

                    ecb.AddComponent(styleEntity, new TrackStyle {
                        Settings = entity,
                        Spacing = styleData.Spacing,
                        Threshold = styleData.Threshold
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
