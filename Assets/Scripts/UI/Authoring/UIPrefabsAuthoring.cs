using UnityEngine;
using Unity.Entities;

namespace KexEdit.UI {
    public class UIPrefabsAuthoring : MonoBehaviour {
        public GameObject Gizmo;

        private class Baker : Baker<UIPrefabsAuthoring> {
            public override void Bake(UIPrefabsAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                var gizmoEntity = GetEntity(authoring.Gizmo, TransformUsageFlags.Dynamic);
                AddComponent<KeyframeGizmoPrefab>(entity, gizmoEntity);
            }
        }
    }
}
