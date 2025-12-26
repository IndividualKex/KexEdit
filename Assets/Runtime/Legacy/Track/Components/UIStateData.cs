using Unity.Entities;
using KexEdit.Persistence;

namespace KexEdit.Legacy {
    public struct UIStateData : IComponentData {
        public UIStateChunk Value;
    }
}
