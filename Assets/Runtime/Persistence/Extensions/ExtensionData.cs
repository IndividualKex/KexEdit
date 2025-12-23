using System;
using Unity.Collections;

namespace KexEdit.Persistence {
    public struct ExtensionData : IDisposable {
        public UIMetadataChunk UIMetadata;
        public bool HasUIMetadata;

        public static ExtensionData Create(Allocator allocator) {
            return new ExtensionData {
                UIMetadata = new UIMetadataChunk(allocator),
                HasUIMetadata = false
            };
        }

        public void Dispose() {
            UIMetadata.Dispose();
        }
    }
}
