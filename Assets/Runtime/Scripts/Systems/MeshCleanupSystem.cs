using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class MeshCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var mesh in GameObject.FindObjectsByType<NodeMesh>(FindObjectsSortMode.None)) {
                if (EntityManager.HasComponent<MeshReference>(mesh.Node) &&
                    EntityManager.GetComponentData<MeshReference>(mesh.Node).Value == mesh) continue;
                GameObject.Destroy(mesh.gameObject);
            }
        }
    }
}
