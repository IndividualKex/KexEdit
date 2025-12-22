using Unity.Entities;
using CoasterAggregate = KexEdit.Coaster.Coaster;

namespace KexEdit.Legacy {
    public struct CoasterData : IComponentData {
        public CoasterAggregate Value;
    }
}
