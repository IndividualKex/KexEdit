using Unity.Entities;

namespace KexEdit.Legacy {
    public struct Train : IComponentData {
        public float Distance;
        public int Facing;
        public bool Enabled;
        public bool Kinematic;
    }
}
