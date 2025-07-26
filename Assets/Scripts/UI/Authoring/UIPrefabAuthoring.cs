using UnityEngine;
using Unity.Entities;

namespace KexEdit.UI {
    public class UIPrefabAuthoring : MonoBehaviour {
        public GameObject Gizmo;

        private class Baker : Baker<UIPrefabAuthoring> {
            public override void Bake(UIPrefabAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                var gizmoEntity = GetEntity(authoring.Gizmo, TransformUsageFlags.Dynamic);
                AddComponent<KeyframeGizmoPrefabReference>(entity, gizmoEntity);
            }
        }
    }
}
