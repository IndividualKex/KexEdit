using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class CartMeshCleanupSystem : SystemBase {
        protected override void OnUpdate() {
            foreach (var mesh in GameObject.FindObjectsByType<CartMesh>(FindObjectsSortMode.None)) {
                if (EntityManager.HasComponent<CartMeshReference>(mesh.Cart) &&
                    EntityManager.GetComponentData<CartMeshReference>(mesh.Cart).Value == mesh) continue;
                GameObject.Destroy(mesh.gameObject);
            }
        }
    }
}
