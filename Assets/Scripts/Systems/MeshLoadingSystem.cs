using Unity.Entities;
using KexEdit.UI;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MeshLoadingSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var (meshReference, entity) in SystemAPI.Query<MeshReference>().WithEntityAccess()) {
                if (meshReference.Loaded || meshReference.FilePath.IsEmpty || meshReference.Value != null) continue;
                meshReference.Loaded = true;
                ImportManager.ImportGltfFileAsync(meshReference.FilePath.ToString(), managedMesh => {
                    meshReference.Value = managedMesh;
                    managedMesh.Node = entity;
                });
            }
        }
    }
}
