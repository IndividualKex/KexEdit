using Unity.Entities;

namespace KexEdit {
    public struct Cart : IComponentData {
        public Entity Section;
        public float Position;
        public bool Enabled;
        public bool Kinematic;
    }
}
