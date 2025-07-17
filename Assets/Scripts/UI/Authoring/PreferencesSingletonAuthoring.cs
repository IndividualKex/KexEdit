using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    public class PreferencesSingletonAuthoring : MonoBehaviour {
        private class Baker : Baker<PreferencesSingletonAuthoring> {
            public override void Bake(PreferencesSingletonAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PreferencesSingleton>(entity);
            }
        }
    }
}
