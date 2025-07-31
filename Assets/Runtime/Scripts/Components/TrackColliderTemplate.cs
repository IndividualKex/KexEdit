using Unity.Entities;
using Unity.Physics;

namespace KexEdit {
    public struct TrackColliderTemplate : IComponentData {
        public EntityArchetype Archetype;
        public BlobAssetReference<Collider> ColliderBlob;
    }
}
