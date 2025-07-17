using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    public class CartAuthoring : MonoBehaviour {
        private class Baker : Baker<CartAuthoring> {
            public override void Bake(CartAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Cart {
                    Position = 1f,
                    Active = true,
                    Kinematic = false,
                });
                AddComponent(entity, new CartStyleReference {
                    StyleIndex = 0,
                    Version = 0
                });
                AddComponentObject(entity, new CartMeshReference());
            }
        }
    }
}
