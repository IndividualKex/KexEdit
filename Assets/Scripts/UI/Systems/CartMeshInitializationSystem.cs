using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    [UpdateAfter(typeof(CartStyleAssetLoadingSystem))]
    public partial class CartMeshInitializationSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<CartStyleSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var styleSettings = SystemAPI.ManagedAPI.GetSingleton<CartStyleSettings>();

            foreach (var (style, mesh, coaster, entity) in SystemAPI
                .Query<RefRW<CartStyleReference>, CartMeshReference, CoasterReference>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasComponent<EditorCoasterTag>(coaster) ||
                    style.ValueRO.Version == styleSettings.Version) continue;

                if (mesh.Value != null) {
                    Object.Destroy(mesh.Value.gameObject);
                    mesh.Value = null;
                }

                var cartStyle = styleSettings.Styles[style.ValueRO.StyleIndex];
                if (cartStyle.Mesh == null) continue;

                mesh.Value = Object.Instantiate(cartStyle.Mesh).AddComponent<CartMesh>();
                mesh.Value.Cart = entity;
                mesh.Value.gameObject.SetActive(true);

                if (SystemAPI.HasComponent<RenderTag>(entity)) {
                    var renderTag = SystemAPI.GetComponent<RenderTag>(entity);
                    if ((renderTag.Type & RenderTagType.Playhead) != 0) {
                        var playheadGizmoMaterial = Resources.Load<Material>("PlayheadGizmo");
                        var renderers = mesh.Value.GetComponentsInChildren<Renderer>();
                        foreach (var renderer in renderers) {
                            renderer.sharedMaterial = playheadGizmoMaterial;
                        }
                    }
                }

                style.ValueRW.Version = styleSettings.Version;
            }
        }
    }
}
