using UnityEngine;
using Unity.Entities;

namespace KexEdit {
    public class CartStyleGlobalSettingsAuthoring : MonoBehaviour {
        public Material PlayheadGizmoMaterial;

        private class Baker : Baker<CartStyleGlobalSettingsAuthoring> {
            public override void Bake(CartStyleGlobalSettingsAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponentObject(entity, new CartStyleGlobalSettings {
                    PlayheadGizmoMaterial = authoring.PlayheadGizmoMaterial,
                });

                AddComponentObject(entity, new CartStyleSettings());
            }
        }
    }
}
