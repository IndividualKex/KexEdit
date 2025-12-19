using System;

namespace KexEdit.Persistence {
    [AttributeUsage(AttributeTargets.Class)]
    public class ChunkExtensionAttribute : Attribute {
        public string ChunkType { get; }

        public ChunkExtensionAttribute(string chunkType) {
            ChunkType = chunkType;
        }
    }
}
