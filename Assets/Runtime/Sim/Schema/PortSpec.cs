using System;
using Unity.Burst;

namespace KexEdit.Sim.Schema {
    [BurstCompile]
    public readonly struct PortSpec : IEquatable<PortSpec> {
        public readonly PortDataType DataType;
        public readonly byte LocalIndex;

        public PortSpec(PortDataType dataType, byte localIndex = 0) {
            DataType = dataType;
            LocalIndex = localIndex;
        }

        public static PortSpec Invalid => new((PortDataType)255, 255);

        public bool IsValid => (byte)DataType != 255 || LocalIndex != 255;

        [BurstCompile]
        public uint ToEncoded() => ((uint)(byte)DataType << 8) | LocalIndex;

        [BurstCompile]
        public static void FromEncoded(uint encoded, out PortSpec result) =>
            result = new((PortDataType)(byte)(encoded >> 8), (byte)(encoded & 0xFF));

        public bool Equals(PortSpec other) =>
            DataType == other.DataType && LocalIndex == other.LocalIndex;

        public override bool Equals(object obj) =>
            obj is PortSpec other && Equals(other);

        public override int GetHashCode() =>
            ((int)DataType << 8) | LocalIndex;

        public static bool operator ==(PortSpec left, PortSpec right) => left.Equals(right);
        public static bool operator !=(PortSpec left, PortSpec right) => !left.Equals(right);
    }
}
