using Unity.Entities;

namespace KexEdit.Legacy {
    public struct DistanceFollower : IComponentData {
        public float Distance;
        public bool Active;
    }
}
