using UnityEngine;
using Unity.Entities;

namespace KexEdit {
    public class KeyframeGizmoPrefabReferenceAuthoring : MonoBehaviour {
        public GameObject Gizmo;

        private class Baker : Baker<KeyframeGizmoPrefabReferenceAuthoring> {
            public override void Bake(KeyframeGizmoPrefabReferenceAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                var gizmoEntity = GetEntity(authoring.Gizmo, TransformUsageFlags.Dynamic);
                AddComponent<KeyframeGizmoPrefabReference>(entity, gizmoEntity);
            }
        }
    }
}
