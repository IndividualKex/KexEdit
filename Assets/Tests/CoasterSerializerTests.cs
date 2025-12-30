using KexEdit.Sim.Schema;
using KexEdit.Persistence;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Document.Document;
using Duration = KexEdit.Legacy.Duration;
using DurationType = KexEdit.Legacy.DurationType;
using InterpolationType = KexEdit.Sim.InterpolationType;
using Keyframe = KexEdit.Sim.Keyframe;
using NodeMeta = KexEdit.Document.NodeMeta;

namespace Tests {
    [TestFixture]
    public class CoasterSerializerTests {
        [Test]
        public void EmptyCoaster_RoundTrip_Succeeds() {
            var original = Coaster.Create(Allocator.Temp);

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

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

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

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

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

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

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

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

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

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

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

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
            ulong durKey = Coaster.InputKey(nodeId, NodeMeta.Duration);
            original.Scalars[durKey] = 5.5f;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsTrue(loaded.Scalars.TryGetValue(durKey, out float durationValue));
            Assert.AreEqual(5.5f, durationValue, 0.001f);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Steering_RoundTrip_PreservesFlags() {
            var original = Coaster.Create(Allocator.Temp);
            var node1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);
            var node2 = original.Graph.AddNode((uint)NodeType.Force, new float2(100, 0));
            ulong steeringKey1 = Coaster.InputKey(node1, NodeMeta.Steering);
            ulong steeringKey2 = Coaster.InputKey(node2, NodeMeta.Steering);
            original.Flags[steeringKey1] = 1;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsTrue(loaded.Flags.TryGetValue(steeringKey1, out int s1) && s1 == 1);
            Assert.IsFalse(loaded.Flags.TryGetValue(steeringKey2, out int s2) && s2 == 1);

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

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

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

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.AreEqual(1, loaded.Graph.NodeIds.Length);
            Assert.IsTrue(loaded.Scalars.TryGetValue(nodeId, out float value));
            Assert.AreEqual(42f, value, 0.001f);

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void PropertyOverrides_RoundTrip_PreservesDrivenFlag() {
            var original = Coaster.Create(Allocator.Temp);
            var n1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);
            var n2 = original.Graph.AddNode((uint)NodeType.Geometric, new float2(100, 0));

            ulong drivenKey1 = Coaster.InputKey(n1, NodeMeta.Driven);
            ulong drivenKey2 = Coaster.InputKey(n2, NodeMeta.Driven);

            original.Flags[drivenKey1] = 1;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsTrue(loaded.Flags.TryGetValue(drivenKey1, out int d1) && d1 == 1, "Node 1 should have Driven flag");
            Assert.IsFalse(loaded.Flags.TryGetValue(drivenKey2, out int d2) && d2 == 1, "Node 2 should not have Driven flag");

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void PropertyOverrides_RoundTrip_PreservesOverrideHeart() {
            var original = Coaster.Create(Allocator.Temp);
            var n1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            ulong heartKey = Coaster.InputKey(n1, NodeMeta.OverrideHeart);
            original.Flags[heartKey] = 1;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsTrue(loaded.Flags.TryGetValue(heartKey, out int h) && h == 1, "OverrideHeart flag should be preserved");

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void PropertyOverrides_RoundTrip_PreservesOverrideFriction() {
            var original = Coaster.Create(Allocator.Temp);
            var n1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            ulong frictionKey = Coaster.InputKey(n1, NodeMeta.OverrideFriction);
            original.Flags[frictionKey] = 1;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsTrue(loaded.Flags.TryGetValue(frictionKey, out int f) && f == 1, "OverrideFriction flag should be preserved");

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void PropertyOverrides_RoundTrip_PreservesOverrideResistance() {
            var original = Coaster.Create(Allocator.Temp);
            var n1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            ulong resistanceKey = Coaster.InputKey(n1, NodeMeta.OverrideResistance);
            original.Flags[resistanceKey] = 1;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsTrue(loaded.Flags.TryGetValue(resistanceKey, out int r) && r == 1, "OverrideResistance flag should be preserved");

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void PropertyOverrides_RoundTrip_PreservesOverrideTrackStyle() {
            var original = Coaster.Create(Allocator.Temp);
            var n1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            ulong trackStyleKey = Coaster.InputKey(n1, NodeMeta.OverrideTrackStyle);
            original.Flags[trackStyleKey] = 1;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsTrue(loaded.Flags.TryGetValue(trackStyleKey, out int t) && t == 1, "OverrideTrackStyle flag should be preserved");

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void PropertyOverrides_RoundTrip_PreservesAllOverrides() {
            var original = Coaster.Create(Allocator.Temp);
            var n1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);

            ulong drivenKey = Coaster.InputKey(n1, NodeMeta.Driven);
            ulong heartKey = Coaster.InputKey(n1, NodeMeta.OverrideHeart);
            ulong frictionKey = Coaster.InputKey(n1, NodeMeta.OverrideFriction);
            ulong resistanceKey = Coaster.InputKey(n1, NodeMeta.OverrideResistance);
            ulong trackStyleKey = Coaster.InputKey(n1, NodeMeta.OverrideTrackStyle);

            original.Flags[drivenKey] = 1;
            original.Flags[heartKey] = 1;
            original.Flags[frictionKey] = 1;
            original.Flags[resistanceKey] = 1;
            original.Flags[trackStyleKey] = 1;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsTrue(loaded.Flags.TryGetValue(drivenKey, out int d) && d == 1, "Driven flag should be preserved");
            Assert.IsTrue(loaded.Flags.TryGetValue(heartKey, out int h) && h == 1, "OverrideHeart flag should be preserved");
            Assert.IsTrue(loaded.Flags.TryGetValue(frictionKey, out int f) && f == 1, "OverrideFriction flag should be preserved");
            Assert.IsTrue(loaded.Flags.TryGetValue(resistanceKey, out int r) && r == 1, "OverrideResistance flag should be preserved");
            Assert.IsTrue(loaded.Flags.TryGetValue(trackStyleKey, out int t) && t == 1, "OverrideTrackStyle flag should be preserved");

            loaded.Dispose();
            original.Dispose();
        }

        [Test]
        public void Priority_And_Render_RoundTrip_PreservesValues() {
            var original = Coaster.Create(Allocator.Temp);
            var n1 = original.Graph.AddNode((uint)NodeType.Force, float2.zero);
            var n2 = original.Graph.AddNode((uint)NodeType.Geometric, new float2(100, 0));
            var n3 = original.Graph.AddNode((uint)NodeType.Curved, new float2(200, 0));

            ulong priorityKey1 = Coaster.InputKey(n1, NodeMeta.Priority);
            ulong priorityKey2 = Coaster.InputKey(n2, NodeMeta.Priority);
            ulong priorityKey3 = Coaster.InputKey(n3, NodeMeta.Priority);
            ulong renderKey1 = Coaster.InputKey(n1, NodeMeta.Render);
            ulong renderKey2 = Coaster.InputKey(n2, NodeMeta.Render);
            ulong renderKey3 = Coaster.InputKey(n3, NodeMeta.Render);

            original.Scalars[priorityKey2] = 5f;
            original.Scalars[priorityKey3] = -2f;
            original.Flags[renderKey2] = 1;

            using var writer = new ChunkWriter(Allocator.Temp);
            CoasterSerializer.Write(writer, in original);
            var data = writer.ToArray();

            var reader = new ChunkReader(data);
            var loaded = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.IsFalse(loaded.Scalars.ContainsKey(priorityKey1));
            Assert.IsTrue(loaded.Scalars.TryGetValue(priorityKey2, out float priority2));
            Assert.AreEqual(5f, priority2, 0.001f);
            Assert.IsTrue(loaded.Scalars.TryGetValue(priorityKey3, out float priority3));
            Assert.AreEqual(-2f, priority3, 0.001f);
            Assert.IsFalse(loaded.Flags.TryGetValue(renderKey1, out int r1) && r1 == 1);
            Assert.IsTrue(loaded.Flags.TryGetValue(renderKey2, out int r2) && r2 == 1);
            Assert.IsFalse(loaded.Flags.TryGetValue(renderKey3, out int r3) && r3 == 1);

            loaded.Dispose();
            original.Dispose();
        }
    }
}
