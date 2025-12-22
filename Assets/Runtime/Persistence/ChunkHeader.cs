using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;

namespace KexEdit.Persistence {
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ChunkHeader {
        public const int Size = 12;

        public readonly FixedString32Bytes Type;
        public readonly uint Version;
        public readonly uint Length;

        public ChunkHeader(FixedString32Bytes type, uint version, uint length) {
            Type = type;
            Version = version;
            Length = length;
        }

        public ChunkHeader(string type, uint version, uint length) {
            Type = new FixedString32Bytes(type);
            Version = version;
            Length = length;
        }

        public string TypeString => Type.ToString();
    }
}
