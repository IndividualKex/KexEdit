using KexEdit.Sim.Schema;
using KexEdit.Persistence;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Document.Document;

namespace Tests {
    [TestFixture]
    public class ViewStateSerializationTests {
        [Test]
        public void UIStateChunk_RoundTrip_PreservesViewState() {
            var original = UIStateChunk.Create(Allocator.Temp);
            original.TimelineOffset = 123.5f;
            original.TimelineZoom = 2.5f;
            original.GraphPanX = 100f;
            original.GraphPanY = -200f;
            original.GraphZoom = 1.5f;
            original.CameraPosition = new float3(1, 2, 3);
            original.CameraTargetPosition = new float3(4, 5, 6);
            original.CameraDistance = 50f;
            original.CameraTargetDistance = 55f;
            original.CameraPitch = 30f;
            original.CameraTargetPitch = 35f;
            original.CameraYaw = 45f;
            original.CameraTargetYaw = 50f;
            original.CameraSpeedMultiplier = 2f;

            var writer = new ChunkWriter(Allocator.Temp);
            UIExtensionCodec.Write(ref writer, in original);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            UIExtensionCodec.TryRead(ref reader, Allocator.Temp, out var result);
            data.Dispose();

            Assert.AreEqual(original.TimelineOffset, result.TimelineOffset, 0.001f);
            Assert.AreEqual(original.TimelineZoom, result.TimelineZoom, 0.001f);
            Assert.AreEqual(original.GraphPanX, result.GraphPanX, 0.001f);
            Assert.AreEqual(original.GraphPanY, result.GraphPanY, 0.001f);
            Assert.AreEqual(original.GraphZoom, result.GraphZoom, 0.001f);
            Assert.AreEqual(original.CameraPosition.x, result.CameraPosition.x, 0.001f);
            Assert.AreEqual(original.CameraPosition.y, result.CameraPosition.y, 0.001f);
            Assert.AreEqual(original.CameraPosition.z, result.CameraPosition.z, 0.001f);
            Assert.AreEqual(original.CameraTargetPosition.x, result.CameraTargetPosition.x, 0.001f);
            Assert.AreEqual(original.CameraTargetPosition.y, result.CameraTargetPosition.y, 0.001f);
            Assert.AreEqual(original.CameraTargetPosition.z, result.CameraTargetPosition.z, 0.001f);
            Assert.AreEqual(original.CameraDistance, result.CameraDistance, 0.001f);
            Assert.AreEqual(original.CameraTargetDistance, result.CameraTargetDistance, 0.001f);
            Assert.AreEqual(original.CameraPitch, result.CameraPitch, 0.001f);
            Assert.AreEqual(original.CameraTargetPitch, result.CameraTargetPitch, 0.001f);
            Assert.AreEqual(original.CameraYaw, result.CameraYaw, 0.001f);
            Assert.AreEqual(original.CameraTargetYaw, result.CameraTargetYaw, 0.001f);
            Assert.AreEqual(original.CameraSpeedMultiplier, result.CameraSpeedMultiplier, 0.001f);

            result.Dispose();
            original.Dispose();
        }

        [Test]
        public void UIStateChunk_Default_HasSensibleValues() {
            var chunk = UIStateChunk.Create(Allocator.Temp);

            Assert.AreEqual(0f, chunk.TimelineOffset);
            Assert.AreEqual(1f, chunk.TimelineZoom);
            Assert.AreEqual(0f, chunk.GraphPanX);
            Assert.AreEqual(0f, chunk.GraphPanY);
            Assert.AreEqual(1f, chunk.GraphZoom);
            Assert.AreEqual(50f, chunk.CameraDistance);
            Assert.AreEqual(30f, chunk.CameraPitch);
            Assert.AreEqual(1f, chunk.CameraSpeedMultiplier);

            chunk.Dispose();
        }

        [Test]
        public void UIStateChunk_MissingChunk_ReturnsDefault() {
            var coaster = Coaster.Create(Allocator.Temp);
            coaster.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in coaster);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            bool found = UIExtensionCodec.TryRead(ref reader, Allocator.Temp, out var result);
            data.Dispose();
            coaster.Dispose();

            Assert.False(found);
            Assert.AreEqual(1f, result.TimelineZoom);
            Assert.AreEqual(50f, result.CameraDistance);

            result.Dispose();
        }

        [Test]
        public void CoasterWithUIState_RoundTrip_PreservesAll() {
            var original = Coaster.Create(Allocator.Temp);
            var node1 = original.Graph.AddNode((uint)NodeType.Anchor, float2.zero);
            var node2 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var uiState = UIStateChunk.Create(Allocator.Temp);
            uiState.NodePositions[node1] = new float2(100, 50);
            uiState.NodePositions[node2] = new float2(300, 50);
            uiState.TimelineOffset = 15f;
            uiState.TimelineZoom = 3f;
            uiState.GraphPanX = -50f;
            uiState.GraphPanY = 100f;
            uiState.GraphZoom = 2f;
            uiState.CameraPosition = new float3(10, 20, 30);
            uiState.CameraTargetPosition = new float3(0, 5, 0);
            uiState.CameraDistance = 75f;
            uiState.CameraTargetDistance = 75f;
            uiState.CameraPitch = 45f;
            uiState.CameraTargetPitch = 45f;
            uiState.CameraYaw = 90f;
            uiState.CameraTargetYaw = 90f;
            uiState.CameraSpeedMultiplier = 1.5f;

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
            Assert.AreEqual(15f, loadedState.TimelineOffset, 0.001f);
            Assert.AreEqual(3f, loadedState.TimelineZoom, 0.001f);
            Assert.AreEqual(-50f, loadedState.GraphPanX, 0.001f);
            Assert.AreEqual(75f, loadedState.CameraDistance, 0.001f);

            loadedState.Dispose();
            loaded.Dispose();
            uiState.Dispose();
            original.Dispose();
        }

        [Test]
        public void UIStateChunk_WithUnknownExtensions_StillLoads() {
            var uiState = UIStateChunk.Create(Allocator.Temp);
            uiState.TimelineOffset = 42f;
            uiState.TimelineZoom = 2f;
            uiState.CameraDistance = 100f;
            uiState.CameraTargetDistance = 100f;
            uiState.CameraPitch = 45f;
            uiState.CameraTargetPitch = 45f;

            var coaster = Coaster.Create(Allocator.Temp);
            coaster.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in coaster);

            writer.BeginChunk("UNKN", 1);
            writer.WriteUInt(999);
            writer.EndChunk();

            UIExtensionCodec.Write(ref writer, in uiState);

            writer.BeginChunk("FUTR", 99);
            writer.WriteFloat(3.14f);
            writer.WriteFloat(2.71f);
            writer.EndChunk();

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            CoasterSerializer.Read(ref reader, Allocator.Temp).Dispose();
            bool found = UIExtensionCodec.TryRead(ref reader, Allocator.Temp, out var result);
            data.Dispose();
            coaster.Dispose();
            uiState.Dispose();

            Assert.True(found);
            Assert.AreEqual(42f, result.TimelineOffset, 0.001f);
            Assert.AreEqual(100f, result.CameraDistance, 0.001f);

            result.Dispose();
        }
    }
}
