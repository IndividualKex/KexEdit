using KexEdit.Sim;
using KexEdit.Graph.Typed;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Nodes.Anchor;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Document.Document;
using Duration = KexEdit.Legacy.Duration;
using DurationType = KexEdit.Legacy.DurationType;
using NodeMeta = KexEdit.Document.NodeMeta;

public class CoasterTests {
    [Test]
    public void Create_InitializesEmptyCoaster() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            Assert.AreEqual(0, coaster.Graph.NodeCount);
            Assert.AreEqual(0, coaster.Keyframes.Keyframes.Length);
            Assert.AreEqual(0, coaster.Scalars.Count);
            Assert.AreEqual(0, coaster.Vectors.Count);
            Assert.AreEqual(0, coaster.Flags.Count);
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Dispose_CleansUpAllContainers() {
        var coaster = Coaster.Create(Allocator.Temp);
        coaster.Dispose();

        Assert.IsFalse(coaster.Graph.NodeIds.IsCreated);
        Assert.IsFalse(coaster.Keyframes.Keyframes.IsCreated);
        Assert.IsFalse(coaster.Scalars.IsCreated);
    }

    [Test]
    public void AddAnchorNode_StoresPositionAndRotation() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            var position = new float3(10f, 20f, 30f);
            float roll = 0.1f, pitch = 0.2f, yaw = 0.3f;
            uint nodeId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out var inputPorts, out _, Allocator.Temp);

            ulong posKey = Coaster.InputKey(nodeId, AnchorPorts.Position);
            ulong rollKey = Coaster.InputKey(nodeId, AnchorPorts.Roll);
            ulong pitchKey = Coaster.InputKey(nodeId, AnchorPorts.Pitch);
            ulong yawKey = Coaster.InputKey(nodeId, AnchorPorts.Yaw);

            coaster.Vectors[posKey] = position;
            coaster.Scalars[rollKey] = roll;
            coaster.Scalars[pitchKey] = pitch;
            coaster.Scalars[yawKey] = yaw;

            Assert.AreEqual(position, coaster.Vectors[posKey]);
            Assert.AreEqual(roll, coaster.Scalars[rollKey]);
            Assert.AreEqual(pitch, coaster.Scalars[pitchKey]);
            Assert.AreEqual(yaw, coaster.Scalars[yawKey]);

            inputPorts.Dispose();
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void AddForceNode_StoresDurationAndKeyframes() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Force, float2.zero, out _, out _, Allocator.Temp);
            float durationValue = 5f;
            ulong durKey = Coaster.InputKey(nodeId, NodeMeta.Duration);
            ulong durTypeKey = Coaster.InputKey(nodeId, NodeMeta.DurationType);
            coaster.Scalars[durKey] = durationValue;
            coaster.Flags[durTypeKey] = 0;

            var keyframes = new NativeArray<Keyframe>(2, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 1f);
            keyframes[1] = new Keyframe(5f, 2f);
            coaster.Keyframes.Set(nodeId, PropertyId.NormalForce, keyframes);
            keyframes.Dispose();

            Assert.AreEqual(durationValue, coaster.Scalars[durKey]);
            Assert.AreEqual(0, coaster.Flags[durTypeKey]);
            Assert.IsTrue(coaster.Keyframes.TryGet(nodeId, PropertyId.NormalForce, out var retrieved));
            Assert.AreEqual(2, retrieved.Length);
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void GeometricNode_SteeringSupportedViaFlags() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Geometric, float2.zero, out _, out _, Allocator.Temp);
            ulong steeringKey = Coaster.InputKey(nodeId, NodeMeta.Steering);

            Assert.IsFalse(coaster.Flags.ContainsKey(steeringKey));

            coaster.Flags[steeringKey] = 1;
            Assert.IsTrue(coaster.Flags.TryGetValue(steeringKey, out int val) && val == 1);

            coaster.Flags.Remove(steeringKey);
            Assert.IsFalse(coaster.Flags.ContainsKey(steeringKey));
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void ScalarNode_StoresValue() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Scalar, float2.zero, out _, out _, Allocator.Temp);
            ulong key = Coaster.InputKey(nodeId, 0);
            coaster.Scalars[key] = 42.5f;

            Assert.AreEqual(42.5f, coaster.Scalars[key]);
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void VectorNode_StoresValue() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Vector, float2.zero, out _, out _, Allocator.Temp);
            ulong key = Coaster.InputKey(nodeId, 0);
            var value = new float3(1f, 2f, 3f);
            coaster.Vectors[key] = value;

            Assert.AreEqual(value, coaster.Vectors[key]);
        } finally {
            coaster.Dispose();
        }
    }

}
