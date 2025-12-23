using System;
using Unity.Collections;
using Unity.Mathematics;
using CoasterAggregate = KexEdit.App.Coaster.Coaster;

namespace KexEdit.Legacy.Serialization {
    public struct ClipboardData : IDisposable {
        public CoasterAggregate Coaster;
        public NativeArray<float2> NodeOffsets;
        public float2 Center;

        public void Dispose() {
            Coaster.Dispose();
            NodeOffsets.Dispose();
        }
    }
}
