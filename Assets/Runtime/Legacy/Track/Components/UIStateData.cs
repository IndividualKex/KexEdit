using Unity.Entities;
using KexEdit.App.Persistence;

namespace KexEdit.Legacy {
    public struct UIStateData : IComponentData {
        public UIStateChunk Value;
    }
}
