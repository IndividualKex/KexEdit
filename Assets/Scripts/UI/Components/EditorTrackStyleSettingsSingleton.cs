using Unity.Entities;

namespace KexEdit.UI {
    public struct EditorTrackStyleSettingsSingleton : IComponentData {
        public Entity Settings;
        public bool Dirty;
    }
}
