using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MeshLoadingSystem : SystemBase {
        protected override void OnUpdate() {
            using var entitiesToLoad = new NativeList<Entity>(Allocator.Temp);
            foreach (var (meshReference, render, entity) in SystemAPI.Query<MeshReference, Render>().WithEntityAccess()) {
                if (meshReference.Loaded ||
                    meshReference.FilePath.IsEmpty ||
                    meshReference.Value != Entity.Null) continue;
                entitiesToLoad.Add(entity);
            }

            foreach (var entity in entitiesToLoad) {
                ref var meshReference = ref SystemAPI.GetComponentRW<MeshReference>(entity).ValueRW;
                meshReference.Loaded = true;
                string filePath = meshReference.FilePath.ToString();
                if (filePath.EndsWith(".glb") || filePath.EndsWith(".gltf")) {
                    ImportManager.ImportGltfFile(filePath, EntityManager, 0, result => {
                        if (!SystemAPI.HasComponent<MeshReference>(entity)) return;
                        ref var meshReference = ref SystemAPI.GetComponentRW<MeshReference>(entity).ValueRW;
                        meshReference.Value = result;
                        EntityManager.AddComponentData(result, new NodeMesh { Node = entity });
                    });
                }
                else if (filePath.EndsWith(".obj")) {
                    var result = ImportManager.ImportObjFile(filePath, EntityManager, 0);
                    if (result != Entity.Null) {
                        meshReference.Value = result;
                        EntityManager.AddComponentData(result, new NodeMesh { Node = entity });
                    }
                }
                else {
                    UnityEngine.Debug.LogError($"Unsupported file type: {filePath}");
                }
            }
        }
    }
}
