using KexEdit.Core;
using KexEdit.NodeGraph;
using KexEdit.Nodes;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Coaster.Coaster;
using Duration = KexEdit.Coaster.Duration;
using DurationType = KexEdit.Coaster.DurationType;

public class CoasterTests {
    [Test]
    public void Create_InitializesEmptyCoaster() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            Assert.AreEqual(0, coaster.Graph.NodeCount);
            Assert.AreEqual(0, coaster.Keyframes.Keyframes.Length);
            Assert.AreEqual(0, coaster.Scalars.Count);
            Assert.AreEqual(0, coaster.Vectors.Count);
            Assert.AreEqual(0, coaster.Durations.Count);
            Assert.AreEqual(0, coaster.Steering.Count);
            Assert.AreEqual(0, coaster.Anchors.Count);
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
            var rotation = new float3(0.1f, 0.2f, 0.3f);
            uint nodeId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out _, Allocator.Temp);

            coaster.Vectors[nodeId] = position;
            coaster.SetRotation(nodeId, rotation);

            Assert.AreEqual(position, coaster.Vectors[nodeId]);
            Assert.AreEqual(rotation, coaster.GetRotation(nodeId));
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void AddForceNode_StoresDurationAndKeyframes() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Force, float2.zero, out _, out _, Allocator.Temp);
            var duration = new Duration(5f, DurationType.Time);
            coaster.Durations[nodeId] = duration;

            var keyframes = new NativeArray<Keyframe>(2, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 1f);
            keyframes[1] = new Keyframe(5f, 2f);
            coaster.Keyframes.Set(nodeId, PropertyId.NormalForce, keyframes);
            keyframes.Dispose();

            Assert.AreEqual(duration.Value, coaster.Durations[nodeId].Value);
            Assert.AreEqual(duration.Type, coaster.Durations[nodeId].Type);
            Assert.IsTrue(coaster.Keyframes.TryGet(nodeId, PropertyId.NormalForce, out var retrieved));
            Assert.AreEqual(2, retrieved.Length);
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void GeometricNode_SteeringSupportedViaHashSet() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Geometric, float2.zero, out _, out _, Allocator.Temp);

            Assert.IsFalse(coaster.Steering.Contains(nodeId));

            coaster.Steering.Add(nodeId);
            Assert.IsTrue(coaster.Steering.Contains(nodeId));

            coaster.Steering.Remove(nodeId);
            Assert.IsFalse(coaster.Steering.Contains(nodeId));
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void ScalarNode_StoresValue() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Scalar, float2.zero, out _, out _, Allocator.Temp);
            coaster.Scalars[nodeId] = 42.5f;

            Assert.AreEqual(42.5f, coaster.Scalars[nodeId]);
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void VectorNode_StoresValue() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Vector, float2.zero, out _, out _, Allocator.Temp);
            var value = new float3(1f, 2f, 3f);
            coaster.Vectors[nodeId] = value;

            Assert.AreEqual(value, coaster.Vectors[nodeId]);
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Anchor_StoresInitialPointState() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out _, Allocator.Temp);
            var anchor = Point.Default;
            coaster.Anchors[nodeId] = anchor;

            Assert.AreEqual(anchor.SpinePosition, coaster.Anchors[nodeId].SpinePosition);
            Assert.AreEqual(anchor.Velocity, coaster.Anchors[nodeId].Velocity);
        } finally {
            coaster.Dispose();
        }
    }
}
