using KexEdit.Nodes;
using KexEdit.Persistence;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Coaster.Coaster;

namespace Tests {
    [TestFixture]
    public class ViewStateSerializationTests {
        [Test]
        public void ViewStateChunk_RoundTrip_PreservesAllFields() {
            var original = new ViewStateChunk {
                TimelineOffset = 123.5f,
                TimelineZoom = 2.5f,
                GraphPanX = 100f,
                GraphPanY = -200f,
                GraphZoom = 1.5f,
                CameraPosition = new float3(1, 2, 3),
                CameraTargetPosition = new float3(4, 5, 6),
                CameraDistance = 50f,
                CameraTargetDistance = 55f,
                CameraPitch = 30f,
                CameraTargetPitch = 35f,
                CameraYaw = 45f,
                CameraTargetYaw = 50f,
                CameraSpeedMultiplier = 2f
            };

            var writer = new ChunkWriter(Allocator.Temp);
            ViewStateCodec.WriteChunk(ref writer, in original);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            reader.TryReadHeader(out var header);

            Assert.AreEqual(ExtensionSchema.ViewStateType, header.TypeString);

            var result = new ViewStateChunk {
                TimelineOffset = reader.ReadFloat(),
                TimelineZoom = reader.ReadFloat(),
                GraphPanX = reader.ReadFloat(),
                GraphPanY = reader.ReadFloat(),
                GraphZoom = reader.ReadFloat(),
                CameraPosition = reader.ReadFloat3(),
                CameraTargetPosition = reader.ReadFloat3(),
                CameraDistance = reader.ReadFloat(),
                CameraTargetDistance = reader.ReadFloat(),
                CameraPitch = reader.ReadFloat(),
                CameraTargetPitch = reader.ReadFloat(),
                CameraYaw = reader.ReadFloat(),
                CameraTargetYaw = reader.ReadFloat(),
                CameraSpeedMultiplier = reader.ReadFloat()
            };
            reader.Dispose();
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
        }

        [Test]
        public void ViewStateChunk_Default_HasSensibleValues() {
            var chunk = ViewStateChunk.Default;

            Assert.AreEqual(0f, chunk.TimelineOffset);
            Assert.AreEqual(1f, chunk.TimelineZoom);
            Assert.AreEqual(0f, chunk.GraphPanX);
            Assert.AreEqual(0f, chunk.GraphPanY);
            Assert.AreEqual(1f, chunk.GraphZoom);
            Assert.AreEqual(50f, chunk.CameraDistance);
            Assert.AreEqual(30f, chunk.CameraPitch);
            Assert.AreEqual(1f, chunk.CameraSpeedMultiplier);
        }

        [Test]
        public void ViewStateChunk_MissingChunk_ReturnsDefault() {
            var coaster = Coaster.Create(Allocator.Temp);
            coaster.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in coaster);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            bool found = ViewStateCodec.TryReadFromFile(ref reader, out var result);
            reader.Dispose();
            data.Dispose();
            coaster.Dispose();

            Assert.False(found);
            Assert.AreEqual(ViewStateChunk.Default.TimelineZoom, result.TimelineZoom);
            Assert.AreEqual(ViewStateChunk.Default.CameraDistance, result.CameraDistance);
        }

        [Test]
        public void CoasterWithViewState_RoundTrip_PreservesAll() {
            var original = Coaster.Create(Allocator.Temp);
            var node1 = original.Graph.AddNode((uint)NodeType.Anchor, float2.zero);
            var node2 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var uiMeta = new UIMetadataChunk(Allocator.Temp);
            uiMeta.Positions[node1] = new float2(100, 50);
            uiMeta.Positions[node2] = new float2(300, 50);

            var viewState = new ViewStateChunk {
                TimelineOffset = 15f,
                TimelineZoom = 3f,
                GraphPanX = -50f,
                GraphPanY = 100f,
                GraphZoom = 2f,
                CameraPosition = new float3(10, 20, 30),
                CameraTargetPosition = new float3(0, 5, 0),
                CameraDistance = 75f,
                CameraTargetDistance = 75f,
                CameraPitch = 45f,
                CameraTargetPitch = 45f,
                CameraYaw = 90f,
                CameraTargetYaw = 90f,
                CameraSpeedMultiplier = 1.5f
            };

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            UIMetadataCodec.WriteChunk(ref writer, in uiMeta);
            ViewStateCodec.WriteChunk(ref writer, in viewState);

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);
            reader.Dispose();

            var reader2 = new ChunkReader(data);
            UIMetadataCodec.TryReadFromFile(ref reader2, Allocator.Temp, out var loadedMeta);
            reader2.Dispose();

            var reader3 = new ChunkReader(data);
            ViewStateCodec.TryReadFromFile(ref reader3, out var loadedViewState);
            reader3.Dispose();

            data.Dispose();

            Assert.AreEqual(2, loaded.Graph.NodeIds.Length);
            Assert.AreEqual(2, loadedMeta.Positions.Count);
            Assert.AreEqual(100f, loadedMeta.Positions[node1].x, 0.001f);
            Assert.AreEqual(15f, loadedViewState.TimelineOffset, 0.001f);
            Assert.AreEqual(3f, loadedViewState.TimelineZoom, 0.001f);
            Assert.AreEqual(-50f, loadedViewState.GraphPanX, 0.001f);
            Assert.AreEqual(75f, loadedViewState.CameraDistance, 0.001f);

            loadedMeta.Dispose();
            loaded.Dispose();
            uiMeta.Dispose();
            original.Dispose();
        }

        [Test]
        public void ViewStateChunk_WithUnknownExtensions_StillLoads() {
            var viewState = new ViewStateChunk {
                TimelineOffset = 42f,
                TimelineZoom = 2f,
                GraphPanX = 0f,
                GraphPanY = 0f,
                GraphZoom = 1f,
                CameraPosition = float3.zero,
                CameraTargetPosition = float3.zero,
                CameraDistance = 100f,
                CameraTargetDistance = 100f,
                CameraPitch = 45f,
                CameraTargetPitch = 45f,
                CameraYaw = 0f,
                CameraTargetYaw = 0f,
                CameraSpeedMultiplier = 1f
            };

            var coaster = Coaster.Create(Allocator.Temp);
            coaster.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in coaster);

            writer.BeginChunk("UNKN", 1);
            writer.WriteUInt(999);
            writer.EndChunk();

            ViewStateCodec.WriteChunk(ref writer, in viewState);

            writer.BeginChunk("FUTR", 99);
            writer.WriteFloat(3.14f);
            writer.WriteFloat(2.71f);
            writer.EndChunk();

            var data = writer.ToArray();
            writer.Dispose();

            var reader = new ChunkReader(data);
            bool found = ViewStateCodec.TryReadFromFile(ref reader, out var result);
            reader.Dispose();
            data.Dispose();
            coaster.Dispose();

            Assert.True(found);
            Assert.AreEqual(42f, result.TimelineOffset, 0.001f);
            Assert.AreEqual(100f, result.CameraDistance, 0.001f);
        }
    }
}
