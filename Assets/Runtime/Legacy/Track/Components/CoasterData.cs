using Unity.Entities;
using CoasterAggregate = KexEdit.App.Coaster.Coaster;

namespace KexEdit.Legacy {
    public struct CoasterData : IComponentData {
        public CoasterAggregate Value;
    }
}
