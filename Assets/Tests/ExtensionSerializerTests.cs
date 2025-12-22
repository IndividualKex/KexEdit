using KexEdit.Sim.Schema;
using KexEdit.Persistence;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Document.Document;

namespace Tests {
    [TestFixture]
    public class ExtensionSerializerTests {
        [Test]
        public void UIStateChunk_RoundTrip_PreservesPositions() {
            var original = UIStateChunk.Create(Allocator.Temp);
            original.NodePositions[1] = new float2(100, 200);
            original.NodePositions[2] = new float2(300, 400);

            var writer = new ChunkWriter(Allocator.Temp);
            UIExtensionCodec.Write(ref writer, in original);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            UIExtensionCodec.TryRead(ref reader, Allocator.Temp, out var loaded);
            data.Dispose();

            Assert.AreEqual(2, loaded.NodePositions.Count);
            Assert.AreEqual(100f, loaded.NodePositions[1].x, 0.001f);
            Assert.AreEqual(200f, loaded.NodePositions[1].y, 0.001f);
            Assert.AreEqual(300f, loaded.NodePositions[2].x, 0.001f);
            Assert.AreEqual(400f, loaded.NodePositions[2].y, 0.001f);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void UIStateChunk_Empty_RoundTrips() {
            var original = UIStateChunk.Create(Allocator.Temp);

            var writer = new ChunkWriter(Allocator.Temp);
            UIExtensionCodec.Write(ref writer, in original);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            UIExtensionCodec.TryRead(ref reader, Allocator.Temp, out var loaded);
            data.Dispose();

            Assert.AreEqual(0, loaded.NodePositions.Count);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void CoasterWithUIState_RoundTrip_PreservesAll() {
            var original = Coaster.Create(Allocator.Temp);
            var node1 = original.Graph.AddNode((uint)NodeType.Anchor, float2.zero);
            var node2 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var uiState = UIStateChunk.Create(Allocator.Temp);
            uiState.NodePositions[node1] = new float2(100, 50);
            uiState.NodePositions[node2] = new float2(300, 50);

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            UIExtensionCodec.Write(ref writer, in uiState);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);
            UIExtensionCodec.TryRead(ref reader, Allocator.Temp, out var loadedState);
            data.Dispose();

            Assert.AreEqual(2, loaded.Graph.NodeIds.Length);
            Assert.AreEqual(2, loadedState.NodePositions.Count);
            Assert.AreEqual(100f, loadedState.NodePositions[node1].x, 0.001f);
            Assert.AreEqual(300f, loadedState.NodePositions[node2].x, 0.001f);

            loadedState.Dispose();
            loaded.Dispose();
            uiState.Dispose();
            original.Dispose();
        }

        [Test]
        public void UnknownExtension_Skipped_LoadsSuccessfully() {
            var original = Coaster.Create(Allocator.Temp);
            var nodeId = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var uiState = UIStateChunk.Create(Allocator.Temp);
            uiState.NodePositions[nodeId] = new float2(100, 200);

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);

            writer.BeginChunk("UNKN", 1);
            writer.WriteUInt(999);
            writer.WriteUInt(888);
            writer.EndChunk();

            UIExtensionCodec.Write(ref writer, in uiState);

            writer.BeginChunk("TEST", 2);
            writer.WriteFloat(3.14f);
            writer.EndChunk();

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);
            UIExtensionCodec.TryRead(ref reader, Allocator.Temp, out var loadedState);
            data.Dispose();

            Assert.AreEqual(1, loadedState.NodePositions.Count);
            Assert.AreEqual(100f, loadedState.NodePositions[nodeId].x, 0.001f);

            loadedState.Dispose();
            loaded.Dispose();
            uiState.Dispose();
            original.Dispose();
        }
    }
}
