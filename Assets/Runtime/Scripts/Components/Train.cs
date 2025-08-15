using Unity.Entities;

namespace KexEdit {
    public struct Train : IComponentData {
        public Entity Section;
        public float Position;
        public float TotalLength;
        public int CarCount;
        public bool Enabled;
        public bool Kinematic;
    }
}
