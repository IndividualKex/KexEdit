using System;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Persistence {
    public struct UIMetadataChunk : IDisposable {
        public NativeHashMap<uint, float2> Positions;

        public UIMetadataChunk(Allocator allocator) {
            Positions = new NativeHashMap<uint, float2>(64, allocator);
        }

        public void Dispose() {
            if (Positions.IsCreated) {
                Positions.Dispose();
            }
        }
    }
}
