using Unity.Entities;
using Unity.Physics;

namespace KexEdit {
    public struct ColliderTemplate : IComponentData {
        public EntityArchetype Archetype;
        public BlobAssetReference<Collider> ColliderBlob;
    }
}
