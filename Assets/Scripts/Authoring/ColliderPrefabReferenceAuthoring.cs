using UnityEngine;
using Unity.Entities;

namespace KexEdit {

    public class ColliderPrefabReferenceAuthoring : MonoBehaviour {
        public GameObject Prefab;

        private class Baker : Baker<ColliderPrefabReferenceAuthoring> {
            public override void Bake(ColliderPrefabReferenceAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                var prefabEntity = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic);
                AddComponent<ColliderPrefabReference>(entity, prefabEntity);
            }
        }
    }
}
