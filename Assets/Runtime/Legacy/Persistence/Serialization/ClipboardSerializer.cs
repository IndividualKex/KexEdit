using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Legacy.Serialization {
    public static class ClipboardSerializer {
        public static byte[] Serialize(ref ClipboardData clipboardData) {
            // Calculate total size
            int graphSize = SizeCalculator.CalculateSize(ref clipboardData.Graph);
            int offsetsSize = sizeof(int) + clipboardData.NodeOffsets.Length * (2 * sizeof(float));
            int centerSize = 2 * sizeof(float);
            int totalSize = graphSize + offsetsSize + centerSize;

            var buffer = new NativeArray<byte>(totalSize, Allocator.Temp);
            var writer = new BinaryWriter(buffer);

            // Serialize graph
            int actualGraphSize = GraphSerializer.Serialize(ref clipboardData.Graph, ref buffer);

            // Create a new writer positioned after the graph data
            var remainingBuffer = buffer.GetSubArray(actualGraphSize, buffer.Length - actualGraphSize);
            writer = new BinaryWriter(remainingBuffer);

            // Serialize offsets
            writer.WriteArray(clipboardData.NodeOffsets);

            // Serialize center
            writer.Write(clipboardData.Center);

            int actualSize = actualGraphSize + writer.Position;
            var result = new byte[actualSize];
            buffer.Slice(0, actualSize).CopyTo(result);
            buffer.Dispose();

            return result;
        }

        public static ClipboardData Deserialize(byte[] data) {
            var buffer = new NativeArray<byte>(data, Allocator.Temp);

            // Deserialize graph first
            var clipboardData = new ClipboardData();
            int graphSize = GraphSerializer.Deserialize(ref clipboardData.Graph, ref buffer);

            // Deserialize offsets and center
            var remainingBuffer = buffer.GetSubArray(graphSize, buffer.Length - graphSize);
            var reader = new BinaryReader(remainingBuffer);
            reader.ReadArray(out clipboardData.NodeOffsets, Allocator.Temp);
            clipboardData.Center = reader.Read<float2>();

            buffer.Dispose();
            return clipboardData;
        }
    }
}
