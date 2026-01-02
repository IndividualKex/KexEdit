using KexEdit.Sim;
using KexEdit.Graph.Typed;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Nodes.Anchor;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Coaster = KexEdit.Document.Document;
using NodeMeta = KexEdit.Document.NodeMeta;

public class CoasterEvaluatorTests {
    [Test]
    public void Evaluate_EmptyCoaster_ReturnsEmptyResult() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.AreEqual(0, track.SectionCount);
                Assert.AreEqual(0, track.Points.Length);
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Evaluate_SingleAnchorNode_ProducesOutputAnchor() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint nodeId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out _, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(nodeId, AnchorPorts.Position)] = new float3(10f, 20f, 30f);

            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.AreEqual(0, track.SectionCount, "Anchor nodes don't produce sections");
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Evaluate_AnchorToForce_ProducesPathAndOutputAnchor() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

            uint forceId = coaster.Graph.CreateNode(NodeType.Force, new float2(100f, 0f), out var forceInputs, out _, Allocator.Temp);
            coaster.Scalars[Coaster.InputKey(forceId, NodeMeta.Duration)] = 1f;

            coaster.Graph.AddEdge(anchorOutputs[0], forceInputs[0]);

            anchorOutputs.Dispose();
            forceInputs.Dispose();

            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.IsTrue(track.NodeToSection.TryGetValue(forceId, out int sectionIndex), "Force node should have section");

                var section = track.Sections[sectionIndex];
                Assert.IsTrue(section.IsValid, "Force section should be valid");
                Assert.Greater(section.Length, 1, "Section should have multiple points");

                var lastPoint = track.Points[section.EndIndex];
                Assert.AreNotEqual(0f, lastPoint.HeartPosition.z, "Output position should have moved from start");
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Evaluate_AnchorToGeometric_ProducesPathAndOutputAnchor() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

            uint geoId = coaster.Graph.CreateNode(NodeType.Geometric, new float2(100f, 0f), out var geoInputs, out _, Allocator.Temp);
            coaster.Scalars[Coaster.InputKey(geoId, NodeMeta.Duration)] = 1f;

            coaster.Graph.AddEdge(anchorOutputs[0], geoInputs[0]);

            anchorOutputs.Dispose();
            geoInputs.Dispose();

            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.IsTrue(track.NodeToSection.TryGetValue(geoId, out int sectionIndex), "Geometric node should have section");

                var section = track.Sections[sectionIndex];
                Assert.IsTrue(section.IsValid, "Geometric section should be valid");
                Assert.Greater(section.Length, 1, "Section should have multiple points");
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Evaluate_AnchorToCurved_ProducesPathAndOutputAnchor() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

            uint curvedId = coaster.Graph.CreateNode(NodeType.Curved, new float2(100f, 0f), out var curvedInputs, out _, Allocator.Temp);
            coaster.Scalars[Coaster.InputKey(curvedId, 1)] = 20f;

            coaster.Graph.AddEdge(anchorOutputs[0], curvedInputs[0]);

            anchorOutputs.Dispose();
            curvedInputs.Dispose();

            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.IsTrue(track.NodeToSection.TryGetValue(curvedId, out int sectionIndex), "Curved node should have section");

                var section = track.Sections[sectionIndex];
                Assert.IsTrue(section.IsValid, "Curved section should be valid");
                Assert.Greater(section.Length, 1, "Section should have multiple points");
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Evaluate_AnchorToReverse_ReversesDirection() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

            uint reverseId = coaster.Graph.CreateNode(NodeType.Reverse, new float2(100f, 0f), out var reverseInputs, out _, Allocator.Temp);

            coaster.Graph.AddEdge(anchorOutputs[0], reverseInputs[0]);

            anchorOutputs.Dispose();
            reverseInputs.Dispose();

            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.AreEqual(0, track.SectionCount, "Reverse nodes don't produce sections");
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }


    [Test]
    public void Evaluate_ForceToCopyPath_CopiesPath() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

            uint forceId = coaster.Graph.CreateNode(NodeType.Force, new float2(100f, 0f), out var forceInputs, out var forceOutputs, Allocator.Temp);
            coaster.Scalars[Coaster.InputKey(forceId, NodeMeta.Duration)] = 0.5f;

            uint anchor2Id = coaster.Graph.CreateNode(NodeType.Anchor, new float2(0f, 100f), out _, out var anchor2Outputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchor2Id, AnchorPorts.Position)] = new float3(50f, 10f, 0f);

            uint copyId = coaster.Graph.CreateNode(NodeType.CopyPath, new float2(200f, 0f), out var copyInputs, out _, Allocator.Temp);

            coaster.Graph.AddEdge(anchorOutputs[0], forceInputs[0]);
            coaster.Graph.AddEdge(anchor2Outputs[0], copyInputs[0]);
            coaster.Graph.AddEdge(forceOutputs[1], copyInputs[1]);

            anchorOutputs.Dispose();
            forceInputs.Dispose();
            forceOutputs.Dispose();
            anchor2Outputs.Dispose();
            copyInputs.Dispose();

            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.IsTrue(track.NodeToSection.TryGetValue(copyId, out int sectionIndex), "CopyPath should have section");

                var section = track.Sections[sectionIndex];
                Assert.IsTrue(section.IsValid, "CopyPath section should be valid");
                Assert.Greater(section.Length, 1, "CopyPath should have multiple points");
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Evaluate_Bridge_CreatesBezierPath() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint anchor1Id = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchor1Outputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchor1Id, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

            uint anchor2Id = coaster.Graph.CreateNode(NodeType.Anchor, new float2(0f, 100f), out _, out var anchor2Outputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchor2Id, AnchorPorts.Position)] = new float3(0f, 10f, 50f);

            uint bridgeId = coaster.Graph.CreateNode(NodeType.Bridge, new float2(100f, 0f), out var bridgeInputs, out _, Allocator.Temp);

            coaster.Graph.AddEdge(anchor1Outputs[0], bridgeInputs[0]);
            coaster.Graph.AddEdge(anchor2Outputs[0], bridgeInputs[1]);

            anchor1Outputs.Dispose();
            anchor2Outputs.Dispose();
            bridgeInputs.Dispose();

            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.IsTrue(track.NodeToSection.TryGetValue(bridgeId, out int sectionIndex), "Bridge should have section");

                var section = track.Sections[sectionIndex];
                Assert.IsTrue(section.IsValid, "Bridge section should be valid");
                Assert.Greater(section.Length, 1, "Bridge should have multiple points");
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Evaluate_ScalarNodeInjectsToCurvedRadius() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
            coaster.Vectors[Coaster.InputKey(anchorId, AnchorPorts.Position)] = new float3(0f, 10f, 0f);

            uint scalarId = coaster.Graph.CreateNode(NodeType.Scalar, new float2(-100f, 0f), out _, out var scalarOutputs, Allocator.Temp);
            coaster.Scalars[Coaster.InputKey(scalarId, 0)] = 5f;

            uint curvedId = coaster.Graph.CreateNode(NodeType.Curved, new float2(100f, 0f), out var curvedInputs, out _, Allocator.Temp);

            coaster.Graph.AddEdge(anchorOutputs[0], curvedInputs[0]);
            coaster.Graph.AddEdge(scalarOutputs[0], curvedInputs[1]);

            anchorOutputs.Dispose();
            scalarOutputs.Dispose();
            curvedInputs.Dispose();

            KexEdit.Track.Track.Build(in coaster, Allocator.Temp, 0.1f, 0, out var track);
            try {
                Assert.IsTrue(track.NodeToSection.TryGetValue(curvedId, out int sectionIndex), "Curved node should have section");

                var section = track.Sections[sectionIndex];
                Assert.IsTrue(section.IsValid, "Curved section should be valid");
                Assert.Greater(section.Length, 1, "Section should have multiple points");
            } finally {
                track.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }
}
