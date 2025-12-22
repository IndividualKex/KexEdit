using System.Runtime.InteropServices;
using Unity.Burst;

namespace KexEdit.Sim {
    [BurstCompile]
    public readonly struct PhysicsParams {
        public readonly float HeartOffset;
        public readonly float Friction;
        public readonly float Resistance;
        public readonly float DeltaRoll;
        [MarshalAs(UnmanagedType.U1)]
        public readonly bool Driven;

        public PhysicsParams(
            float heartOffset,
            float friction,
            float resistance,
            float deltaRoll,
            bool driven
        ) {
            HeartOffset = heartOffset;
            Friction = friction;
            Resistance = resistance;
            DeltaRoll = deltaRoll;
            Driven = driven;
        }

        public static PhysicsParams Default => new(1.1f, 0f, 0f, 0f, false);
    }
}
