using Unity.Entities;
using DocumentAggregate = KexEdit.Document.Document;

namespace KexEdit.Legacy {
    public struct CoasterData : IComponentData {
        public DocumentAggregate Value;
    }
}
