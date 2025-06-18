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
            }
        }
    }
}
