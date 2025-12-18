using Unity.Entities;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public struct EditorTrackStyleSettingsSingleton : IComponentData {
        public Entity Settings;
        public bool Dirty;
    }
}
