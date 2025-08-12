using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CartMeshLoadingSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = SystemAPI.QueryBuilder()
                .WithAll<LoadCartMeshEvent>()
                .Build();
            
            RequireForUpdate(_query);
        }

        protected override void OnUpdate() {
            using var events = _query.ToEntityArray(Allocator.Temp);
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < events.Length; i++) {
                var entity = events[i];
                var evt = SystemAPI.GetComponent<LoadCartMeshEvent>(entity);

                if (evt.Target == Entity.Null) {
                    UnityEngine.Debug.LogError("LoadCartMeshEvent missing Target entity");
                    ecb.DestroyEntity(entity);
                    continue;
                }

                if (!SystemAPI.HasComponent<CartMeshReference>(evt.Target)) {
                    UnityEngine.Debug.LogError("Target entity missing CartMeshReference component");
                    ecb.DestroyEntity(entity);
                    continue;
                }

                ref var cartMeshReference = ref SystemAPI.GetComponentRW<CartMeshReference>(evt.Target).ValueRW;
                if (cartMeshReference.Value != Entity.Null) {
                    ecb.DestroyEntity(cartMeshReference.Value);
                }

                Entity cartMesh = EntityManager.Instantiate(evt.Cart);
                cartMeshReference.Value = cartMesh;
                ecb.AddComponent(cartMesh, new CartMesh { Cart = evt.Target });

                ecb.DestroyEntity(entity);
            }
            
            ecb.Playback(EntityManager);
        }
    }
}
