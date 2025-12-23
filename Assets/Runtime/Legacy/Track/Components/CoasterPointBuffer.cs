#if VALIDATE_COASTER_PARITY
using Unity.Entities;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Legacy {
    public struct CoasterPointBuffer : IBufferElementData {
        public CorePoint Point;
        public int Facing;
    }
}
#endif
