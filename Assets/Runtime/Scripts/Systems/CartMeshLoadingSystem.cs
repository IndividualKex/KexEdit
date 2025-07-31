using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CartMeshLoadingSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<LoadCartMeshEvent>();
        }

        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadCartMeshEvent>().WithEntityAccess()) {
                if (evt.Target == Entity.Null) {
                    UnityEngine.Debug.LogError("LoadCartMeshEvent missing Target entity");
                    ecb.DestroyEntity(entity);
                    continue;
                }

                if (!SystemAPI.ManagedAPI.HasComponent<CartMeshReference>(evt.Target)) {
                    UnityEngine.Debug.LogError("Target entity missing CartMeshReference component");
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var cartMeshReference = SystemAPI.ManagedAPI.GetComponent<CartMeshReference>(evt.Target);
                if (cartMeshReference.Value != null) {
                    Object.Destroy(cartMeshReference.Value.gameObject);
                }

                var cartMesh = Object.Instantiate(evt.Cart).AddComponent<CartMesh>();
                cartMesh.Cart = evt.Target;
                cartMeshReference.Value = cartMesh;
                cartMesh.gameObject.SetActive(true);
                cartMesh.gameObject.layer = LayerMask.NameToLayer("Cart");
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }
    }
}
