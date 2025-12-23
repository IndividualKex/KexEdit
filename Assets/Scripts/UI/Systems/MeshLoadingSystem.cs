using KexEdit.Legacy;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MeshLoadingSystem : SystemBase {
        protected override void OnUpdate() {
            using var entitiesToLoad = new NativeList<Entity>(Allocator.Temp);
            foreach (var (meshReference, render, entity) in SystemAPI.Query<NodeMeshReference, Render>().WithEntityAccess()) {
                if (meshReference.Requested ||
                    meshReference.FilePath.IsEmpty ||
                    meshReference.Value != Entity.Null) continue;
                entitiesToLoad.Add(entity);
            }

            foreach (var entity in entitiesToLoad) {
                ref var meshReference = ref SystemAPI.GetComponentRW<NodeMeshReference>(entity).ValueRW;
                meshReference.Requested = true;
                string filePath = meshReference.FilePath.ToString();
                if (filePath.EndsWith(".glb") || filePath.EndsWith(".gltf")) {
                    EntityImporter.ImportGltfFile(filePath, 0, result => {
                        if (!SystemAPI.HasComponent<NodeMeshReference>(entity)) return;
                        ref var meshReference = ref SystemAPI.GetComponentRW<NodeMeshReference>(entity).ValueRW;
                        meshReference.Value = result;
                        EntityManager.AddComponentData(result, new NodeMesh { Node = entity });
                    });
                }
                else if (filePath.EndsWith(".obj")) {
                    EntityImporter.ImportObjFile(filePath, 0, result => {
                        if (!SystemAPI.HasComponent<NodeMeshReference>(entity)) return;
                        ref var meshReference = ref SystemAPI.GetComponentRW<NodeMeshReference>(entity).ValueRW;
                        meshReference.Value = result;
                        EntityManager.AddComponentData(result, new NodeMesh { Node = entity });
                    });
                }
                else {
                    UnityEngine.Debug.LogError($"Unsupported file type: {filePath}");
                }
            }
        }
    }
}
