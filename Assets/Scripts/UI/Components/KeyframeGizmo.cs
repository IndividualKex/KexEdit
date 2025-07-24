using Unity.Entities;

namespace KexEdit.UI {
    public struct KeyframeGizmo : IComponentData {
        public Entity Section;
        public PropertyType PropertyType;
        public uint KeyframeId;
    }
}
