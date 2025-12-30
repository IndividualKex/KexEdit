using KexEdit.Persistence;
using Unity.Collections;
using Unity.Mathematics;
using DocumentAggregate = KexEdit.Document.Document;

namespace KexEdit.Legacy.Serialization {
    public static class ClipboardSerializer {
        public static byte[] Serialize(ref ClipboardData clipboardData) {
            using var writer = new ChunkWriter(Allocator.Temp);

            CoasterSerializer.Write(writer, in clipboardData.Coaster);

            writer.WriteArray(clipboardData.NodeOffsets);
            writer.WriteFloat2(clipboardData.Center);

            using var buffer = writer.ToArray();
            var result = new byte[buffer.Length];
            buffer.CopyTo(result);
            return result;
        }

        public static ClipboardData Deserialize(byte[] data) {
            var buffer = new NativeArray<byte>(data, Allocator.Temp);

            try {
                var reader = new ChunkReader(buffer);
                var coaster = CoasterSerializer.Read(ref reader, Allocator.Persistent);

                var nodeOffsets = reader.ReadArrayWithLength<float2>(Allocator.Persistent);
                var center = reader.ReadFloat2();

                return new ClipboardData {
                    Coaster = coaster,
                    NodeOffsets = nodeOffsets,
                    Center = center
                };
            }
            finally {
                buffer.Dispose();
            }
        }
    }
}
