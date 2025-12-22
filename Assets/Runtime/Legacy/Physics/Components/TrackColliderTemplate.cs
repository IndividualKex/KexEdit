using Unity.Entities;
using Unity.Physics;

namespace KexEdit.Legacy {
    public struct TrackColliderTemplate : IComponentData {
        public EntityArchetype Archetype;
        public BlobAssetReference<Collider> ColliderBlob;
    }
}
