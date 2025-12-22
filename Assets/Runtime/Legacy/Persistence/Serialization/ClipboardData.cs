using System;
using Unity.Collections;
using Unity.Mathematics;
using DocumentAggregate = KexEdit.Document.Document;

namespace KexEdit.Legacy.Serialization {
    public struct ClipboardData : IDisposable {
        public DocumentAggregate Coaster;
        public NativeArray<float2> NodeOffsets;
        public float2 Center;

        public void Dispose() {
            Coaster.Dispose();
            NodeOffsets.Dispose();
        }
    }
}
