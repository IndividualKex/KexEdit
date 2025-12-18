using KexEdit.Coaster;
using KexEdit.Core;
using KexEdit.NodeGraph;
using KexEdit.Nodes;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using DurationType = KexEdit.Coaster.DurationType;

public class CoasterEvaluatorTests {
    [Test]
    public void Evaluate_EmptyCoaster_ReturnsEmptyResult() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.AreEqual(0, result.Paths.Count);
                Assert.AreEqual(0, result.OutputAnchors.Count);
            } finally {
                result.Dispose();
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
            coaster.Vectors[nodeId] = new float3(10f, 20f, 30f);
            coaster.SetRotation(nodeId, new float3(0f, 0f, 0f));

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.AreEqual(1, result.OutputAnchors.Count);
                Assert.IsTrue(result.OutputAnchors.ContainsKey(nodeId));

                var anchor = result.OutputAnchors[nodeId];
                Assert.AreEqual(10f, anchor.SpinePosition.x, 0.001f);
                Assert.AreEqual(20f, anchor.SpinePosition.y, 0.001f);
                Assert.AreEqual(30f, anchor.SpinePosition.z, 0.001f);
            } finally {
                result.Dispose();
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
            coaster.Vectors[anchorId] = new float3(0f, 10f, 0f);
            coaster.SetRotation(anchorId, new float3(0f, 0f, 0f));

            uint forceId = coaster.Graph.CreateNode(NodeType.Force, new float2(100f, 0f), out var forceInputs, out _, Allocator.Temp);
            coaster.Durations[forceId] = new Duration(1f, DurationType.Time);

            coaster.Graph.AddEdge(anchorOutputs[0], forceInputs[0]);

            anchorOutputs.Dispose();
            forceInputs.Dispose();

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.IsTrue(result.OutputAnchors.ContainsKey(forceId), "Force node should have output anchor");
                Assert.IsTrue(result.Paths.ContainsKey(forceId), "Force node should have path");

                var path = result.Paths[forceId];
                Assert.Greater(path.Length, 1, "Path should have multiple points");

                var outputAnchor = result.OutputAnchors[forceId];
                Assert.AreNotEqual(0f, outputAnchor.SpinePosition.z, "Output anchor should have moved from start");
            } finally {
                result.Dispose();
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
            coaster.Vectors[anchorId] = new float3(0f, 10f, 0f);
            coaster.SetRotation(anchorId, new float3(0f, 0f, 0f));

            uint geoId = coaster.Graph.CreateNode(NodeType.Geometric, new float2(100f, 0f), out var geoInputs, out _, Allocator.Temp);
            coaster.Durations[geoId] = new Duration(1f, DurationType.Time);

            coaster.Graph.AddEdge(anchorOutputs[0], geoInputs[0]);

            anchorOutputs.Dispose();
            geoInputs.Dispose();

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.IsTrue(result.OutputAnchors.ContainsKey(geoId), "Geometric node should have output anchor");
                Assert.IsTrue(result.Paths.ContainsKey(geoId), "Geometric node should have path");

                var path = result.Paths[geoId];
                Assert.Greater(path.Length, 1, "Path should have multiple points");
            } finally {
                result.Dispose();
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
            coaster.Vectors[anchorId] = new float3(0f, 10f, 0f);
            coaster.SetRotation(anchorId, new float3(0f, 0f, 0f));

            uint curvedId = coaster.Graph.CreateNode(NodeType.Curved, new float2(100f, 0f), out var curvedInputs, out _, Allocator.Temp);
            coaster.Scalars[curvedId] = 20f;

            coaster.Graph.AddEdge(anchorOutputs[0], curvedInputs[0]);

            anchorOutputs.Dispose();
            curvedInputs.Dispose();

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.IsTrue(result.OutputAnchors.ContainsKey(curvedId), "Curved node should have output anchor");
                Assert.IsTrue(result.Paths.ContainsKey(curvedId), "Curved node should have path");

                var path = result.Paths[curvedId];
                Assert.Greater(path.Length, 1, "Path should have multiple points");
            } finally {
                result.Dispose();
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
            coaster.Vectors[anchorId] = new float3(0f, 10f, 0f);
            coaster.SetRotation(anchorId, new float3(0f, 0f, 0f));

            uint reverseId = coaster.Graph.CreateNode(NodeType.Reverse, new float2(100f, 0f), out var reverseInputs, out _, Allocator.Temp);

            coaster.Graph.AddEdge(anchorOutputs[0], reverseInputs[0]);

            anchorOutputs.Dispose();
            reverseInputs.Dispose();

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.IsTrue(result.OutputAnchors.ContainsKey(reverseId), "Reverse node should have output anchor");

                var inputAnchor = result.OutputAnchors[anchorId];
                var outputAnchor = result.OutputAnchors[reverseId];

                Assert.AreEqual(-inputAnchor.Direction.x, outputAnchor.Direction.x, 0.001f);
                Assert.AreEqual(-inputAnchor.Direction.y, outputAnchor.Direction.y, 0.001f);
                Assert.AreEqual(-inputAnchor.Direction.z, outputAnchor.Direction.z, 0.001f);
            } finally {
                result.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }

    [Test]
    public void Evaluate_ForceToReversePath_ReversesPath() {
        var coaster = Coaster.Create(Allocator.Temp);
        try {
            uint anchorId = coaster.Graph.CreateNode(NodeType.Anchor, float2.zero, out _, out var anchorOutputs, Allocator.Temp);
            coaster.Vectors[anchorId] = new float3(0f, 10f, 0f);

            uint forceId = coaster.Graph.CreateNode(NodeType.Force, new float2(100f, 0f), out var forceInputs, out var forceOutputs, Allocator.Temp);
            coaster.Durations[forceId] = new Duration(0.5f, DurationType.Time);

            uint reversePathId = coaster.Graph.CreateNode(NodeType.ReversePath, new float2(200f, 0f), out var rpInputs, out _, Allocator.Temp);

            coaster.Graph.AddEdge(anchorOutputs[0], forceInputs[0]);
            coaster.Graph.AddEdge(forceOutputs[1], rpInputs[0]);

            anchorOutputs.Dispose();
            forceInputs.Dispose();
            forceOutputs.Dispose();
            rpInputs.Dispose();

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.IsTrue(result.Paths.ContainsKey(reversePathId), "ReversePath should have path");

                var originalPath = result.Paths[forceId];
                var reversedPath = result.Paths[reversePathId];

                Assert.AreEqual(originalPath.Length, reversedPath.Length);
                Assert.AreEqual(originalPath[0].SpinePosition.x, reversedPath[reversedPath.Length - 1].SpinePosition.x, 0.001f);
            } finally {
                result.Dispose();
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
            coaster.Vectors[anchorId] = new float3(0f, 10f, 0f);

            uint forceId = coaster.Graph.CreateNode(NodeType.Force, new float2(100f, 0f), out var forceInputs, out var forceOutputs, Allocator.Temp);
            coaster.Durations[forceId] = new Duration(0.5f, DurationType.Time);

            uint anchor2Id = coaster.Graph.CreateNode(NodeType.Anchor, new float2(0f, 100f), out _, out var anchor2Outputs, Allocator.Temp);
            coaster.Vectors[anchor2Id] = new float3(50f, 10f, 0f);

            uint copyId = coaster.Graph.CreateNode(NodeType.CopyPath, new float2(200f, 0f), out var copyInputs, out _, Allocator.Temp);

            coaster.Graph.AddEdge(anchorOutputs[0], forceInputs[0]);
            coaster.Graph.AddEdge(anchor2Outputs[0], copyInputs[0]);
            coaster.Graph.AddEdge(forceOutputs[1], copyInputs[1]);

            anchorOutputs.Dispose();
            forceInputs.Dispose();
            forceOutputs.Dispose();
            anchor2Outputs.Dispose();
            copyInputs.Dispose();

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.IsTrue(result.Paths.ContainsKey(copyId), "CopyPath should have path");
                Assert.IsTrue(result.OutputAnchors.ContainsKey(copyId), "CopyPath should have output anchor");

                var copyPath = result.Paths[copyId];
                Assert.Greater(copyPath.Length, 1, "CopyPath should have multiple points");
            } finally {
                result.Dispose();
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
            coaster.Vectors[anchor1Id] = new float3(0f, 10f, 0f);
            coaster.SetRotation(anchor1Id, new float3(0f, 0f, 0f));

            uint anchor2Id = coaster.Graph.CreateNode(NodeType.Anchor, new float2(0f, 100f), out _, out _, Allocator.Temp);
            coaster.Vectors[anchor2Id] = new float3(0f, 10f, 50f);
            coaster.SetRotation(anchor2Id, new float3(0f, 0f, 0f));

            uint bridgeId = coaster.Graph.CreateNode(NodeType.Bridge, new float2(100f, 0f), out var bridgeInputs, out _, Allocator.Temp);
            coaster.Anchors[bridgeId] = Point.Default;

            coaster.Graph.AddEdge(anchor1Outputs[0], bridgeInputs[0]);

            anchor1Outputs.Dispose();
            bridgeInputs.Dispose();

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.IsTrue(result.Paths.ContainsKey(bridgeId), "Bridge should have path");
                Assert.IsTrue(result.OutputAnchors.ContainsKey(bridgeId), "Bridge should have output anchor");

                var bridgePath = result.Paths[bridgeId];
                Assert.Greater(bridgePath.Length, 1, "Bridge should have multiple points");
            } finally {
                result.Dispose();
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
            coaster.Vectors[anchorId] = new float3(0f, 10f, 0f);

            uint scalarId = coaster.Graph.CreateNode(NodeType.Scalar, new float2(-100f, 0f), out _, out var scalarOutputs, Allocator.Temp);
            coaster.Scalars[scalarId] = 5f;

            uint curvedId = coaster.Graph.CreateNode(NodeType.Curved, new float2(100f, 0f), out var curvedInputs, out _, Allocator.Temp);

            coaster.Graph.AddEdge(anchorOutputs[0], curvedInputs[0]);
            coaster.Graph.AddEdge(scalarOutputs[0], curvedInputs[1]);

            anchorOutputs.Dispose();
            scalarOutputs.Dispose();
            curvedInputs.Dispose();

            CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);
            try {
                Assert.IsTrue(result.Paths.ContainsKey(curvedId), "Curved node should have path");

                var path = result.Paths[curvedId];
                Assert.Greater(path.Length, 1, "Path should have multiple points");
            } finally {
                result.Dispose();
            }
        } finally {
            coaster.Dispose();
        }
    }
}
