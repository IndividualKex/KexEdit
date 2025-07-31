using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    public partial class MeshCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var mesh in GameObject.FindObjectsByType<NodeMesh>(FindObjectsSortMode.None)) {
                if (SystemAPI.ManagedAPI.HasComponent<MeshReference>(mesh.Node) &&
                    SystemAPI.ManagedAPI.GetComponent<MeshReference>(mesh.Node).Value == mesh) continue;
                GameObject.Destroy(mesh.gameObject);
            }
        }
    }
}
