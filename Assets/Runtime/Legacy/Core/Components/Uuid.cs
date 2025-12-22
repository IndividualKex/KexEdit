using System;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    public struct Uuid {
        public static uint Create() {
            var guid = Guid.NewGuid();
            var hash = (uint)math.abs(guid.GetHashCode());
            var ticks = (uint)(DateTime.UtcNow.Ticks & 0xFFFFFFFF);
            return hash ^ ticks;
        }
    }
}
