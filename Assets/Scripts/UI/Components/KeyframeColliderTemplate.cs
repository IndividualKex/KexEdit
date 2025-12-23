using Unity.Entities;
using Unity.Physics;

namespace KexEdit.UI {
    public struct KeyframeColliderTemplate : IComponentData {
        public EntityArchetype Archetype;
        public BlobAssetReference<Collider> ColliderBlob;
    }
}
