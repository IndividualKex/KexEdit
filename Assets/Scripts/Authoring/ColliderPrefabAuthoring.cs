using UnityEngine;
using Unity.Entities;

namespace KexEdit {
    public class ColliderPrefabAuthoring : MonoBehaviour {
        private class Baker : Baker<ColliderPrefabAuthoring> {
            public override void Bake(ColliderPrefabAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<NodeReference>(entity);
            }
        }
    }
}
