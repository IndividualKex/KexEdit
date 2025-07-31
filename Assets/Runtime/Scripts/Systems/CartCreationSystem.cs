using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CartCreationSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (coaster, entity) in SystemAPI
                .Query<Coaster>()
                .WithNone<CartReference>()
                .WithEntityAccess()
            ) {
                var cartEntity = ecb.CreateEntity();
                ecb.AddComponent(cartEntity, LocalTransform.Identity);
                ecb.AddComponent<CartReference>(entity, cartEntity);
                ecb.AddComponent<CoasterReference>(cartEntity, entity);
                ecb.AddComponent(cartEntity, new Cart {
                    Position = 1f,
                    Enabled = true,
                    Kinematic = false
                });
                ecb.AddComponent(cartEntity, new CartMeshReference());
                ecb.SetName(cartEntity, "Cart");
            }
            ecb.Playback(EntityManager);
        }
    }
}
