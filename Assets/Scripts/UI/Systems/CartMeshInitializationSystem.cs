using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    [UpdateAfter(typeof(CartStyleAssetLoadingSystem))]
    public partial class CartMeshInitializationSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CartStyleReference, CartMeshReference, CoasterReference>()
                .Build();

            RequireForUpdate(_query);
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<CartStyleSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var styleSettings = SystemAPI.ManagedAPI.GetSingleton<CartStyleSettings>();

            using var entities = _query.ToEntityArray(Allocator.Temp);

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++) {
                var entity = entities[i];

                ref var style = ref SystemAPI.GetComponentRW<CartStyleReference>(entity).ValueRW;
                var coaster = SystemAPI.GetComponent<CoasterReference>(entity);

                if (!SystemAPI.HasComponent<EditorCoasterTag>(coaster) ||
                    style.Version == styleSettings.Version) continue;

                ref var mesh = ref SystemAPI.GetComponentRW<CartMeshReference>(entity).ValueRW;

                if (mesh.Value != Entity.Null) {
                    ecb.DestroyEntity(mesh.Value);
                    mesh.Value = Entity.Null;
                }

                var cartStyle = styleSettings.Styles[style.StyleIndex];
                if (cartStyle.Mesh == Entity.Null) continue;

                Entity instance = EntityManager.Instantiate(cartStyle.Mesh);
                ecb.AddComponent(instance, new CartMesh { Cart = entity });
                mesh.Value = instance;

                if (SystemAPI.HasComponent<RenderTag>(entity)) {
                    var renderTag = SystemAPI.GetComponent<RenderTag>(entity);
                    if ((renderTag.Type & RenderTagType.Playhead) != 0) {
                        var playheadGizmoMaterial = Resources.Load<Material>("PlayheadGizmo");
                        ecb.AddComponent(instance, new PendingMaterialUpdate { Material = playheadGizmoMaterial });
                    }
                }

                style.Version = styleSettings.Version;
            }
            ecb.Playback(EntityManager);
        }
    }
}
