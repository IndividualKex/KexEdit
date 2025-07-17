using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(CartMeshLoadingSystem))]
    public partial class CartMeshInitializationSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<CartStyleGlobalSettings>();
            RequireForUpdate<CartStyleSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<CartStyleGlobalSettings>();
            var styleSettings = SystemAPI.ManagedAPI.GetSingleton<CartStyleSettings>();

            foreach (var (style, mesh, entity) in SystemAPI
                .Query<RefRW<CartStyleReference>, CartMeshReference>()
                .WithEntityAccess()
            ) {
                if (style.ValueRO.Version == styleSettings.Version) continue;

                if (mesh.Value != null) {
                    Object.Destroy(mesh.Value.gameObject);
                    mesh.Value = null;
                }

                var cartStyle = styleSettings.Styles[style.ValueRO.StyleIndex];
                if (cartStyle.Mesh == null) continue;

                mesh.Value = Object.Instantiate(cartStyle.Mesh).AddComponent<CartMesh>();
                mesh.Value.Cart = entity;
                mesh.Value.gameObject.SetActive(true);

                if (EntityManager.HasComponent<PlayheadGizmoTag>(entity) && globalSettings.PlayheadGizmoMaterial != null) {
                    var renderers = mesh.Value.GetComponentsInChildren<Renderer>();
                    foreach (var renderer in renderers) {
                        renderer.sharedMaterial = globalSettings.PlayheadGizmoMaterial;
                    }
                }

                style.ValueRW.Version = styleSettings.Version;
            }
        }
    }
}
