using System;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Serialization {
    public struct ClipboardData : IDisposable {
        public SerializedGraph Graph;
        public NativeArray<float2> NodeOffsets;
        public float2 Center;

        public void Dispose() {
            Graph.Dispose();
            NodeOffsets.Dispose();
        }
    }
}
