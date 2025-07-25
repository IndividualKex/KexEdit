using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MeshLoadingSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var (meshReference, render, entity) in SystemAPI.Query<MeshReference, Render>().WithEntityAccess()) {
                if (meshReference.Loaded || 
                    meshReference.FilePath.IsEmpty || 
                    meshReference.Value != null) continue;
                meshReference.Loaded = true;
                string filePath = meshReference.FilePath.ToString();
                if (filePath.EndsWith(".glb") || filePath.EndsWith(".gltf")) {
                    ImportManager.ImportGltfFileAsync(filePath, gameObject => {
                        var nodeMesh = gameObject.AddComponent<NodeMesh>();
                        meshReference.Value = nodeMesh;
                        nodeMesh.Node = entity;
                        gameObject.SetActive(render.Value);
                    });
                }
                else if (filePath.EndsWith(".obj")) {
                    ImportManager.ImportObjFile(filePath, gameObject => {
                        var nodeMesh = gameObject.AddComponent<NodeMesh>();
                        meshReference.Value = nodeMesh;
                        nodeMesh.Node = entity;
                        gameObject.SetActive(render.Value);
                    });
                }
                else {
                    UnityEngine.Debug.LogError($"Unsupported file type: {filePath}");
                }
            }
        }
    }
}
