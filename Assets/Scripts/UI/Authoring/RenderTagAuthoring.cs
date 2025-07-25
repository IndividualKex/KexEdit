using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    public class RenderTagAuthoring : MonoBehaviour {
        public RenderTagType Type;

        private class Baker : Baker<RenderTagAuthoring> {
            public override void Bake(RenderTagAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RenderTag {
                    Type = authoring.Type
                });
            }
        }
    }
}
