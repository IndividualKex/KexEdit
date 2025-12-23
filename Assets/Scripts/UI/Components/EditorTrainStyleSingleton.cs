using Unity.Entities;

namespace KexEdit.UI {
    public struct EditorTrainStyleSingleton : IComponentData {
        public Entity Style;
        public bool Dirty;
    }
}
