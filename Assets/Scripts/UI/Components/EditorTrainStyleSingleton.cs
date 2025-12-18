using Unity.Entities;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public struct EditorTrainStyleSingleton : IComponentData {
        public Entity Style;
        public bool Dirty;
    }
}
