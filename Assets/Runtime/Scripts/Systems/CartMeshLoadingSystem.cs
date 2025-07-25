using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CartMeshLoadingSystem : SystemBase {
        protected override void OnCreate() {
            var cartQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Cart, CartMeshReference>()
                .WithNone<CartMesh>()
                .Build(EntityManager);
            RequireForUpdate(cartQuery);
            RequireForUpdate<LoadCartMeshEvent>();
        }

        protected override void OnUpdate() {
            Entity cartEntity = Entity.Null;
            foreach (var (cart, cartMesh, entity) in SystemAPI
                .Query<Cart, CartMeshReference>()
                .WithEntityAccess()
            ) {
                if (!cart.Active || cart.Kinematic) continue;
                cartEntity = entity;
                break;
            }

            if (cartEntity == Entity.Null) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadCartMeshEvent>().WithEntityAccess()) {
                var cartMeshReference = EntityManager.GetComponentObject<CartMeshReference>(cartEntity);
                if (cartMeshReference.Value != null) {
                    UnityEngine.Debug.LogError("Cart mesh already loaded. Overwriting...");
                }
                var cartMesh = Object.Instantiate(evt.Cart).AddComponent<CartMesh>();
                cartMesh.Cart = cartEntity;
                cartMeshReference.Value = cartMesh;
                cartMesh.gameObject.SetActive(true);
                cartMesh.gameObject.layer = LayerMask.NameToLayer("Cart");
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);
        }
    }
}
