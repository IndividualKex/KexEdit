using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace KexGraph.Tests {
    [TestFixture]
    [Category("Unit")]
    public class GraphSerializationTests {
        [Test]
        public void NextNodeId_DefaultGraph_StartsAtOne() {
            var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(1u, graph.NextNodeId);

            graph.Dispose();
        }

        [Test]
        public void NextPortId_DefaultGraph_StartsAtOne() {
            var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(1u, graph.NextPortId);

            graph.Dispose();
        }

        [Test]
        public void NextEdgeId_DefaultGraph_StartsAtOne() {
            var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(1u, graph.NextEdgeId);

            graph.Dispose();
        }

        [Test]
        public void NextNodeId_AfterAddingNode_Increments() {
            var graph = Graph.Create(Allocator.Temp);

            graph.AddNode(nodeType: 1, position: float2.zero);

            Assert.AreEqual(2u, graph.NextNodeId);

            graph.Dispose();
        }

        [Test]
        public void NextPortId_AfterAddingPort_Increments() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.AddInputPort(nodeId, portType: 1);

            Assert.AreEqual(2u, graph.NextPortId);

            graph.Dispose();
        }

        [Test]
        public void NextEdgeId_AfterAddingEdge_Increments() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint outPort = graph.AddOutputPort(node1, portType: 1);
            uint inPort = graph.AddInputPort(node2, portType: 1);

            graph.AddEdge(outPort, inPort);

            Assert.AreEqual(2u, graph.NextEdgeId);

            graph.Dispose();
        }

        [Test]
        public void RebuildIndexMaps_EmptyGraph_Succeeds() {
            var graph = Graph.Create(Allocator.Temp);

            graph.RebuildIndexMaps();

            Assert.AreEqual(0, graph.NodeCount);
            Assert.AreEqual(0, graph.PortCount);
            Assert.AreEqual(0, graph.EdgeCount);

            graph.Dispose();
        }

        [Test]
        public void RebuildIndexMaps_NodesOnly_RebuildsNodeIndexMap() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 2, position: new float2(10, 10));
            uint node3 = graph.AddNode(nodeType: 3, position: new float2(20, 20));

            graph.RebuildIndexMaps();

            Assert.IsTrue(graph.TryGetNodeIndex(node1, out int idx1));
            Assert.IsTrue(graph.TryGetNodeIndex(node2, out int idx2));
            Assert.IsTrue(graph.TryGetNodeIndex(node3, out int idx3));
            Assert.AreEqual(0, idx1);
            Assert.AreEqual(1, idx2);
            Assert.AreEqual(2, idx3);

            graph.Dispose();
        }

        [Test]
        public void RebuildIndexMaps_NodesAndPorts_RebuildsPortIndexMap() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 2, position: float2.zero);
            uint port1 = graph.AddInputPort(node1, portType: 1);
            uint port2 = graph.AddOutputPort(node1, portType: 2);
            uint port3 = graph.AddInputPort(node2, portType: 1);

            graph.RebuildIndexMaps();

            Assert.IsTrue(graph.TryGetPortIndex(port1, out int pidx1));
            Assert.IsTrue(graph.TryGetPortIndex(port2, out int pidx2));
            Assert.IsTrue(graph.TryGetPortIndex(port3, out int pidx3));
            Assert.AreEqual(0, pidx1);
            Assert.AreEqual(1, pidx2);
            Assert.AreEqual(2, pidx3);

            graph.Dispose();
        }

        [Test]
        public void RebuildIndexMaps_FullGraph_RebuildsAllIndexMaps() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 2, position: float2.zero);
            uint outPort = graph.AddOutputPort(node1, portType: 1);
            uint inPort = graph.AddInputPort(node2, portType: 1);
            uint edge = graph.AddEdge(outPort, inPort);

            graph.RebuildIndexMaps();

            Assert.IsTrue(graph.TryGetNodeIndex(node1, out _));
            Assert.IsTrue(graph.TryGetNodeIndex(node2, out _));
            Assert.IsTrue(graph.TryGetPortIndex(outPort, out _));
            Assert.IsTrue(graph.TryGetPortIndex(inPort, out _));
            Assert.IsTrue(graph.TryGetEdgeIndex(edge, out int eidx));
            Assert.AreEqual(0, eidx);

            graph.Dispose();
        }

        [Test]
        public void SimulateRoundTrip_ComplexGraph_PreservesStructure() {
            var original = Graph.Create(Allocator.Temp);
            uint node1 = original.AddNode(nodeType: 1, position: new float2(0, 0));
            uint node2 = original.AddNode(nodeType: 2, position: new float2(100, 0));
            uint node3 = original.AddNode(nodeType: 3, position: new float2(200, 0));

            uint out1 = original.AddOutputPort(node1, portType: 1);
            uint out2 = original.AddOutputPort(node2, portType: 1);
            uint in2 = original.AddInputPort(node2, portType: 1);
            uint in3a = original.AddInputPort(node3, portType: 1);
            uint in3b = original.AddInputPort(node3, portType: 2);

            uint edge1 = original.AddEdge(out1, in2);
            uint edge2 = original.AddEdge(out2, in3a);

            var restored = Graph.Create(Allocator.Temp);
            restored.NodeIds.CopyFrom(original.NodeIds.AsArray());
            restored.NodeTypes.CopyFrom(original.NodeTypes.AsArray());
            restored.NodePositions.CopyFrom(original.NodePositions.AsArray());
            restored.NodeInputCount.CopyFrom(original.NodeInputCount.AsArray());
            restored.NodeOutputCount.CopyFrom(original.NodeOutputCount.AsArray());

            restored.PortIds.CopyFrom(original.PortIds.AsArray());
            restored.PortTypes.CopyFrom(original.PortTypes.AsArray());
            restored.PortOwners.CopyFrom(original.PortOwners.AsArray());
            restored.PortIsInput.CopyFrom(original.PortIsInput.AsArray());

            restored.EdgeIds.CopyFrom(original.EdgeIds.AsArray());
            restored.EdgeSources.CopyFrom(original.EdgeSources.AsArray());
            restored.EdgeTargets.CopyFrom(original.EdgeTargets.AsArray());

            restored.NextNodeId = original.NextNodeId;
            restored.NextPortId = original.NextPortId;
            restored.NextEdgeId = original.NextEdgeId;

            restored.RebuildIndexMaps();

            Assert.AreEqual(original.NodeCount, restored.NodeCount);
            Assert.AreEqual(original.PortCount, restored.PortCount);
            Assert.AreEqual(original.EdgeCount, restored.EdgeCount);

            Assert.IsTrue(restored.TryGetNodeIndex(node1, out int n1idx));
            Assert.IsTrue(restored.TryGetNodeIndex(node2, out int n2idx));
            Assert.IsTrue(restored.TryGetNodeIndex(node3, out int n3idx));

            Assert.AreEqual(original.NodeTypes[0], restored.NodeTypes[n1idx]);
            Assert.AreEqual(original.NodePositions[1], restored.NodePositions[n2idx]);

            Assert.IsTrue(restored.TryGetEdgeIndex(edge1, out int e1idx));
            Assert.AreEqual(original.EdgeSources[0], restored.EdgeSources[e1idx]);
            Assert.AreEqual(original.EdgeTargets[0], restored.EdgeTargets[e1idx]);

            original.Dispose();
            restored.Dispose();
        }

        [Test]
        public void RebuildIndexMaps_CalledMultipleTimes_Idempotent() {
            var graph = Graph.Create(Allocator.Temp);
            uint node = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.RebuildIndexMaps();
            graph.RebuildIndexMaps();
            graph.RebuildIndexMaps();

            Assert.IsTrue(graph.TryGetNodeIndex(node, out int idx));
            Assert.AreEqual(0, idx);

            graph.Dispose();
        }

        [Test]
        public void RebuildIndexMaps_AfterNodeRemoval_CorrectIndices() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 2, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 3, position: float2.zero);

            graph.RemoveNode(node2);
            graph.RebuildIndexMaps();

            Assert.IsTrue(graph.TryGetNodeIndex(node1, out int idx1));
            Assert.IsTrue(graph.TryGetNodeIndex(node3, out int idx3));
            Assert.IsFalse(graph.TryGetNodeIndex(node2, out _));
            Assert.AreEqual(2, graph.NodeCount);

            graph.Dispose();
        }
    }
}
