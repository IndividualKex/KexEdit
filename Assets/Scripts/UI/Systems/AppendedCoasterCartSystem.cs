using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class AppendedCoasterCartSystem : SystemBase {
        private EntityQuery _editorCoasterQuery;
        private EntityQuery _loadCartMeshEventQuery;

        protected override void OnCreate() {
            _editorCoasterQuery = GetEntityQuery(typeof(Coaster), typeof(EditorCoasterTag));
            _loadCartMeshEventQuery = GetEntityQuery(typeof(LoadCartMeshEvent));
            RequireForUpdate<CartStyleSettings>();
        }

        protected override void OnUpdate() {
            if (_editorCoasterQuery.IsEmpty) return;

            Entity editorCoaster = _editorCoasterQuery.GetSingletonEntity();
            if (!SystemAPI.HasComponent<CartReference>(editorCoaster)) return;

            Entity editorCart = SystemAPI.GetComponent<CartReference>(editorCoaster).Value;
            if (editorCart == Entity.Null) return;
            if (!SystemAPI.HasComponent<CartStyleReference>(editorCart)) return;

            var styleSettings = SystemAPI.ManagedAPI.GetSingleton<CartStyleSettings>();
            var editorCartStyleRef = SystemAPI.GetComponent<CartStyleReference>(editorCart);
            
            if (editorCartStyleRef.StyleIndex >= styleSettings.Styles.Count) return;
            
            var cartStyle = styleSettings.Styles[editorCartStyleRef.StyleIndex];
            if (cartStyle.Mesh == null) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            using var existing = new NativeHashSet<Entity>(_loadCartMeshEventQuery.CalculateEntityCount(), Allocator.Temp);
            foreach (var evt in SystemAPI.Query<LoadCartMeshEvent>()) {
                existing.Add(evt.Target);
            }

            foreach (var (coaster, entity) in SystemAPI
                .Query<RefRO<Coaster>>()
                .WithAll<AppendedCoasterTag>()
                .WithEntityAccess()) {

                if (!SystemAPI.HasComponent<CartReference>(entity)) continue;

                Entity cart = SystemAPI.GetComponent<CartReference>(entity).Value;
                if (cart == Entity.Null) continue;
                if (!SystemAPI.ManagedAPI.HasComponent<CartMeshReference>(cart)) continue;

                var cartMesh = SystemAPI.ManagedAPI.GetComponent<CartMeshReference>(cart);

                bool needsStyleReference = false;
                bool needsCartMesh = cartMesh.Value == null;
                
                if (SystemAPI.HasComponent<CartStyleReference>(cart)) {
                    var cartStyleRef = SystemAPI.GetComponent<CartStyleReference>(cart);
                    if (cartStyleRef.StyleIndex != editorCartStyleRef.StyleIndex || 
                        cartStyleRef.Version != styleSettings.Version) {
                        needsStyleReference = true;
                        needsCartMesh = true;
                    }
                } else {
                    needsStyleReference = true;
                    needsCartMesh = true;
                }

                if (needsStyleReference) {
                    if (SystemAPI.HasComponent<CartStyleReference>(cart)) {
                        ecb.SetComponent(cart, new CartStyleReference {
                            StyleIndex = editorCartStyleRef.StyleIndex,
                            Version = styleSettings.Version
                        });
                    } else {
                        ecb.AddComponent(cart, new CartStyleReference {
                            StyleIndex = editorCartStyleRef.StyleIndex,
                            Version = styleSettings.Version
                        });
                    }
                }

                if (needsCartMesh && !existing.Contains(cart)) {
                    if (cartMesh.Value != null) {
                        Object.Destroy(cartMesh.Value.gameObject);
                        cartMesh.Value = null;
                    }

                    var loadEventEntity = ecb.CreateEntity();
                    ecb.AddComponent(loadEventEntity, new LoadCartMeshEvent {
                        Target = cart,
                        Cart = cartStyle.Mesh
                    });
                    ecb.SetName(loadEventEntity, "Load Cart Mesh Event");
                }
            }

            ecb.Playback(EntityManager);
        }
    }
}
