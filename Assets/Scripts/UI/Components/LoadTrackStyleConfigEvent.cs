using Unity.Entities;

namespace KexEdit.UI {
    public class LoadTrackStyleConfigEvent : IComponentData {
        public Entity Target;
        public string ConfigFilename;
    }
}
