using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    public class PlayheadGizmoAuthoring : MonoBehaviour {
        private class Baker : Baker<PlayheadGizmoAuthoring> {
            public override void Bake(PlayheadGizmoAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Cart {
                    Position = 1f,
                    Active = false,
                    Kinematic = true,
                });
                AddComponent(entity, new CartStyleReference {
                    StyleIndex = 0,
                    Version = 0
                });
                AddComponentObject(entity, new CartMeshReference());
                AddComponent<PlayheadGizmoTag>(entity);
            }
        }
    }
}
