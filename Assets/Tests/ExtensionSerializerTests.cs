using KexEdit.Nodes;
using KexEdit.Persistence;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Coaster.Coaster;

namespace Tests {
    [TestFixture]
    public class ExtensionSerializerTests {
        [Test]
        public void UIMetadataChunk_RoundTrip_PreservesPositions() {
            var original = new UIMetadataChunk(Allocator.Temp);
            original.Positions[1] = new float2(100, 200);
            original.Positions[2] = new float2(300, 400);

            var writer = new ChunkWriter(Allocator.Temp);
            writer.BeginChunk(ExtensionSchema.UIMetadataType, ExtensionSchema.UIMetadataVersion);
            writer.WriteInt(original.Positions.Count);
            foreach (var kvp in original.Positions) {
                writer.WriteUInt(kvp.Key);
                writer.WriteFloat2(kvp.Value);
            }
            writer.EndChunk();

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            reader.TryReadHeader(out var header);

            var loaded = new UIMetadataChunk(Allocator.Temp);
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                uint nodeId = reader.ReadUInt();
                var position = reader.ReadFloat2();
                loaded.Positions[nodeId] = position;
            }
            reader.Dispose();
            data.Dispose();

            Assert.AreEqual(2, loaded.Positions.Count);
            Assert.AreEqual(100f, loaded.Positions[1].x, 0.001f);
            Assert.AreEqual(200f, loaded.Positions[1].y, 0.001f);
            Assert.AreEqual(300f, loaded.Positions[2].x, 0.001f);
            Assert.AreEqual(400f, loaded.Positions[2].y, 0.001f);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void UIMetadataChunk_Empty_RoundTrips() {
            var original = new UIMetadataChunk(Allocator.Temp);

            var writer = new ChunkWriter(Allocator.Temp);
            UIMetadataCodec.WriteChunk(ref writer, in original);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            reader.TryReadHeader(out var header);

            var loaded = new UIMetadataChunk(Allocator.Temp);
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                uint nodeId = reader.ReadUInt();
                var position = reader.ReadFloat2();
                loaded.Positions[nodeId] = position;
            }
            reader.Dispose();
            data.Dispose();

            Assert.AreEqual(0, loaded.Positions.Count);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void CoasterWithExtensions_RoundTrip_PreservesAll() {
            var original = Coaster.Create(Allocator.Temp);
            var node1 = original.Graph.AddNode((uint)NodeType.Anchor, float2.zero);
            var node2 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var uiMeta = new UIMetadataChunk(Allocator.Temp);
            uiMeta.Positions[node1] = new float2(100, 50);
            uiMeta.Positions[node2] = new float2(300, 50);

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            UIMetadataCodec.WriteChunk(ref writer, in uiMeta);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);
            reader.Dispose();

            var reader2 = new ChunkReader(data);
            UIMetadataCodec.TryReadFromFile(ref reader2, Allocator.Temp, out var loadedMeta);
            reader2.Dispose();
            data.Dispose();

            Assert.AreEqual(2, loaded.Graph.NodeIds.Length);
            Assert.AreEqual(2, loadedMeta.Positions.Count);
            Assert.AreEqual(100f, loadedMeta.Positions[node1].x, 0.001f);
            Assert.AreEqual(300f, loadedMeta.Positions[node2].x, 0.001f);

            loadedMeta.Dispose();
            loaded.Dispose();
            uiMeta.Dispose();
            original.Dispose();
        }

        [Test]
        public void UnknownExtension_Skipped_LoadsSuccessfully() {
            var original = Coaster.Create(Allocator.Temp);
            var nodeId = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var uiMeta = new UIMetadataChunk(Allocator.Temp);
            uiMeta.Positions[nodeId] = new float2(100, 200);

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);

            writer.BeginChunk("UNKN", 1);
            writer.WriteUInt(999);
            writer.WriteUInt(888);
            writer.EndChunk();

            UIMetadataCodec.WriteChunk(ref writer, in uiMeta);

            writer.BeginChunk("TEST", 2);
            writer.WriteFloat(3.14f);
            writer.EndChunk();

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);
            reader.Dispose();

            var reader2 = new ChunkReader(data);
            UIMetadataCodec.TryReadFromFile(ref reader2, Allocator.Temp, out var loadedMeta);
            reader2.Dispose();
            data.Dispose();

            Assert.AreEqual(1, loadedMeta.Positions.Count);
            Assert.AreEqual(100f, loadedMeta.Positions[nodeId].x, 0.001f);

            loadedMeta.Dispose();
            loaded.Dispose();
            uiMeta.Dispose();
            original.Dispose();
        }
    }
}
