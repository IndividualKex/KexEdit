using Unity.Entities;

namespace KexEdit {
    public struct DistanceFollower : IComponentData {
        public float Distance;
        public bool Active;
    }
}
