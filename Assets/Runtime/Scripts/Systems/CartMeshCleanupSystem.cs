using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    public partial class CartMeshCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var mesh in GameObject.FindObjectsByType<CartMesh>(FindObjectsSortMode.None)) {
                if (SystemAPI.ManagedAPI.HasComponent<CartMeshReference>(mesh.Cart) &&
                    SystemAPI.ManagedAPI.GetComponent<CartMeshReference>(mesh.Cart).Value == mesh) continue;
                GameObject.Destroy(mesh.gameObject);
            }
        }
    }
}
