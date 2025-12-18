using Unity.Entities;
using Unity.Physics;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public struct KeyframeColliderTemplate : IComponentData {
        public EntityArchetype Archetype;
        public BlobAssetReference<Collider> ColliderBlob;
    }
}
