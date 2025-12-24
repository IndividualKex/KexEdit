using KexEdit.Nodes;
using KexEdit.Persistence;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Coaster.Coaster;
using Duration = KexEdit.Coaster.Duration;
using DurationType = KexEdit.Coaster.DurationType;
using InterpolationType = KexEdit.Core.InterpolationType;
using Keyframe = KexEdit.Core.Keyframe;

namespace Tests {
    [TestFixture]
    public class CoasterSerializerTests {
        [Test]
        public void EmptyCoaster_RoundTrip_Succeeds() {
            var original = Coaster.Create(Allocator.Temp);

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.AreEqual(0, loaded.Graph.NodeIds.Length);
            Assert.AreEqual(0, loaded.Scalars.Count);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void SingleNode_RoundTrip_PreservesGraph() {
            var original = Coaster.Create(Allocator.Temp);
            var nodeId = original.Graph.AddNode((uint)NodeType.Force, new float2(100, 200));

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.AreEqual(1, loaded.Graph.NodeIds.Length);
            Assert.AreEqual(nodeId, loaded.Graph.NodeIds[0]);
            Assert.AreEqual((uint)NodeType.Force, loaded.Graph.NodeTypes[0]);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void NodesWithPorts_RoundTrip_PreservesPorts() {
            var original = Coaster.Create(Allocator.Temp);
            var node1 = original.Graph.AddNode((uint)NodeType.Anchor, float2.zero);
            var node2 = original.Graph.AddNode((uint)NodeType.Force, new float2(200, 0));

            var outPort = original.Graph.AddOutputPort(node1, 1);
            var inPort = original.Graph.AddInputPort(node2, 1);
            original.Graph.AddEdge(outPort, inPort);

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.AreEqual(2, loaded.Graph.NodeIds.Length);
            Assert.AreEqual(2, loaded.Graph.PortIds.Length);
            Assert.AreEqual(1, loaded.Graph.EdgeIds.Length);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Scalars_RoundTrip_PreservesValues() {
            var original = Coaster.Create(Allocator.Temp);
            var nodeId = original.Graph.AddNode((uint)NodeType.Force, float2.zero);
            original.Scalars[nodeId] = 42.5f;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.IsTrue(loaded.Scalars.TryGetValue(nodeId, out float value));
            Assert.AreEqual(42.5f, value, 0.001f);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Vectors_RoundTrip_PreservesValues() {
            var original = Coaster.Create(Allocator.Temp);
            var nodeId = original.Graph.AddNode((uint)NodeType.Anchor, float2.zero);
            original.Vectors[nodeId] = new float3(1, 2, 3);

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.IsTrue(loaded.Vectors.TryGetValue(nodeId, out float3 value));
            Assert.AreEqual(1f, value.x, 0.001f);
            Assert.AreEqual(2f, value.y, 0.001f);
            Assert.AreEqual(3f, value.z, 0.001f);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Scalars_RoundTrip_PreservesRotationValues() {
            var original = Coaster.Create(Allocator.Temp);
            uint rollPortId = 100u, pitchPortId = 101u, yawPortId = 102u;
            original.Scalars[rollPortId] = 10f;
            original.Scalars[pitchPortId] = 20f;
            original.Scalars[yawPortId] = 30f;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.AreEqual(10f, loaded.Scalars[rollPortId], 0.001f);
            Assert.AreEqual(20f, loaded.Scalars[pitchPortId], 0.001f);
            Assert.AreEqual(30f, loaded.Scalars[yawPortId], 0.001f);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Durations_RoundTrip_PreservesValues() {
            var original = Coaster.Create(Allocator.Temp);
            var nodeId = original.Graph.AddNode((uint)NodeType.Force, float2.zero);
            original.Durations[nodeId] = new Duration(5.5f, DurationType.Time);

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.IsTrue(loaded.Durations.TryGetValue(nodeId, out Duration duration));
            Assert.AreEqual(5.5f, duration.Value, 0.001f);
            Assert.AreEqual(DurationType.Time, duration.Type);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Steering_RoundTrip_PreservesFlags() {
            var original = Coaster.Create(Allocator.Temp);
            var node1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);
            var node2 = original.Graph.AddNode((uint)NodeType.Force, new float2(100, 0));
            original.Steering.Add(node1);

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.IsTrue(loaded.Steering.Contains(node1));
            Assert.IsFalse(loaded.Steering.Contains(node2));

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Keyframes_RoundTrip_PreservesCurves() {
            var original = Coaster.Create(Allocator.Temp);
            var nodeId = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            var keyframes = new NativeArray<Keyframe>(2, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 1f, InterpolationType.Linear, InterpolationType.Linear, 0, 0, 0.333f, 0.333f);
            keyframes[1] = new Keyframe(1f, 2f, InterpolationType.Bezier, InterpolationType.Bezier, -1, 1, 0.5f, 0.5f);
            original.Keyframes.Set(nodeId, PropertyId.RollSpeed, in keyframes);
            keyframes.Dispose();

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.IsTrue(loaded.Keyframes.TryGet(nodeId, PropertyId.RollSpeed, out var loadedKeyframes));
            Assert.AreEqual(2, loadedKeyframes.Length);
            Assert.AreEqual(0f, loadedKeyframes[0].Time, 0.001f);
            Assert.AreEqual(1f, loadedKeyframes[0].Value, 0.001f);
            Assert.AreEqual(1f, loadedKeyframes[1].Time, 0.001f);
            Assert.AreEqual(2f, loadedKeyframes[1].Value, 0.001f);
            Assert.AreEqual(InterpolationType.Bezier, loadedKeyframes[1].InInterpolation);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void FileHeader_CorrectMagic() {
            var original = Coaster.Create(Allocator.Temp);

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            Assert.AreEqual((byte)'K', data[0]);
            Assert.AreEqual((byte)'E', data[1]);
            Assert.AreEqual((byte)'X', data[2]);
            Assert.AreEqual((byte)'D', data[3]);

            original.Dispose();
        }

        [Test]
        public void UnknownChunk_Skipped_LoadsSuccessfully() {
            var original = Coaster.Create(Allocator.Temp);
            var nodeId = original.Graph.AddNode((uint)NodeType.Force, float2.zero);
            original.Scalars[nodeId] = 42f;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);

            // Manually inject an unknown chunk
            writer.BeginChunk("UNKN", 1);
            writer.WriteUInt(999);
            writer.EndChunk();

            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.AreEqual(1, loaded.Graph.NodeIds.Length);
            Assert.IsTrue(loaded.Scalars.TryGetValue(nodeId, out float value));
            Assert.AreEqual(42f, value, 0.001f);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Priority_And_Render_RoundTrip_PreservesValues() {
            var original = Coaster.Create(Allocator.Temp);
            var n1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);
            var n2 = original.Graph.AddNode((uint)NodeType.Geometric, new float2(100, 0));
            var n3 = original.Graph.AddNode((uint)NodeType.Curved, new float2(200, 0));

            original.Priority[n2] = 5;
            original.Priority[n3] = -2;
            original.Render.Add(n2);

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            using var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.IsFalse(loaded.Priority.ContainsKey(n1));
            Assert.IsTrue(loaded.Priority.TryGetValue(n2, out int priority2));
            Assert.AreEqual(5, priority2);
            Assert.IsTrue(loaded.Priority.TryGetValue(n3, out int priority3));
            Assert.AreEqual(-2, priority3);
            Assert.IsFalse(loaded.Render.Contains(n1));
            Assert.IsTrue(loaded.Render.Contains(n2));
            Assert.IsFalse(loaded.Render.Contains(n3));

            loaded.Dispose();
            original.Dispose();
        }
    }
}
