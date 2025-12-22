using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Graph.Tests {
    [TestFixture]
    [Category("Unit")]
    public class GraphEdgeTests {
        [Test]
        public void AddEdge_ValidPorts_IncreasesEdgeCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint outputPort = graph.AddOutputPort(node1, portType: 1);
            uint inputPort = graph.AddInputPort(node2, portType: 1);

            uint edgeId = graph.AddEdge(outputPort, inputPort);

            Assert.AreEqual(1, graph.EdgeCount);
            Assert.AreNotEqual(0u, edgeId);
            graph.Dispose();
        }

        [Test]
        public void AddEdge_GeneratesUniqueIds() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint out2 = graph.AddOutputPort(node1, portType: 1);
            uint in1 = graph.AddInputPort(node2, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);

            uint edge1 = graph.AddEdge(out1, in1);
            uint edge2 = graph.AddEdge(out2, in2);

            Assert.AreNotEqual(edge1, edge2);
            graph.Dispose();
        }

        [Test]
        public void AddEdge_StoresCorrectSourceAndTarget() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint outputPort = graph.AddOutputPort(node1, portType: 1);
            uint inputPort = graph.AddInputPort(node2, portType: 1);

            uint edgeId = graph.AddEdge(outputPort, inputPort);
            graph.TryGetEdgeIndex(edgeId, out int index);

            Assert.AreEqual(outputPort, graph.EdgeSources[index]);
            Assert.AreEqual(inputPort, graph.EdgeTargets[index]);
            graph.Dispose();
        }

        [Test]
        public void AddEdge_InvalidSourcePort_ReturnsZero() {
            var graph = Graph.Create(Allocator.Temp);
            uint node = graph.AddNode(nodeType: 1, position: float2.zero);
            uint inputPort = graph.AddInputPort(node, portType: 1);

            uint edgeId = graph.AddEdge(999u, inputPort);

            Assert.AreEqual(0u, edgeId);
            Assert.AreEqual(0, graph.EdgeCount);
            graph.Dispose();
        }

        [Test]
        public void AddEdge_InvalidTargetPort_ReturnsZero() {
            var graph = Graph.Create(Allocator.Temp);
            uint node = graph.AddNode(nodeType: 1, position: float2.zero);
            uint outputPort = graph.AddOutputPort(node, portType: 1);

            uint edgeId = graph.AddEdge(outputPort, 999u);

            Assert.AreEqual(0u, edgeId);
            Assert.AreEqual(0, graph.EdgeCount);
            graph.Dispose();
        }

        [Test]
        public void RemoveEdge_DecreasesEdgeCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint outputPort = graph.AddOutputPort(node1, portType: 1);
            uint inputPort = graph.AddInputPort(node2, portType: 1);
            uint edgeId = graph.AddEdge(outputPort, inputPort);

            graph.RemoveEdge(edgeId);

            Assert.AreEqual(0, graph.EdgeCount);
            graph.Dispose();
        }

        [Test]
        public void RemoveEdge_InvalidEdge_DoesNothing() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint outputPort = graph.AddOutputPort(node1, portType: 1);
            uint inputPort = graph.AddInputPort(node2, portType: 1);
            graph.AddEdge(outputPort, inputPort);

            graph.RemoveEdge(999u);

            Assert.AreEqual(1, graph.EdgeCount);
            graph.Dispose();
        }

        [Test]
        public void RemoveEdge_MiddleEdge_MaintainsOtherEdges() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint out2 = graph.AddOutputPort(node1, portType: 1);
            uint out3 = graph.AddOutputPort(node1, portType: 1);
            uint in1 = graph.AddInputPort(node2, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);
            uint in3 = graph.AddInputPort(node2, portType: 1);

            uint edge1 = graph.AddEdge(out1, in1);
            uint edge2 = graph.AddEdge(out2, in2);
            uint edge3 = graph.AddEdge(out3, in3);

            graph.RemoveEdge(edge2);

            Assert.AreEqual(2, graph.EdgeCount);
            Assert.IsTrue(graph.TryGetEdgeIndex(edge1, out _));
            Assert.IsFalse(graph.TryGetEdgeIndex(edge2, out _));
            Assert.IsTrue(graph.TryGetEdgeIndex(edge3, out _));
            graph.Dispose();
        }

        [Test]
        public void GetOutgoingEdges_NodeWithOutgoingEdges_ReturnsCorrectEdges() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint out2 = graph.AddOutputPort(node1, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);
            uint in3 = graph.AddInputPort(node3, portType: 1);

            uint edge1 = graph.AddEdge(out1, in2);
            uint edge2 = graph.AddEdge(out2, in3);

            graph.GetOutgoingEdges(node1, out var edges, Allocator.Temp);

            Assert.AreEqual(2, edges.Length);
            Assert.Contains(edge1, edges.ToArray());
            Assert.Contains(edge2, edges.ToArray());

            edges.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetOutgoingEdges_NodeWithNoOutgoingEdges_ReturnsEmptyArray() {
            var graph = Graph.Create(Allocator.Temp);
            uint node = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.GetOutgoingEdges(node, out var edges, Allocator.Temp);

            Assert.AreEqual(0, edges.Length);

            edges.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetIncomingEdges_NodeWithIncomingEdges_ReturnsCorrectEdges() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint out2 = graph.AddOutputPort(node2, portType: 1);
            uint in3a = graph.AddInputPort(node3, portType: 1);
            uint in3b = graph.AddInputPort(node3, portType: 1);

            uint edge1 = graph.AddEdge(out1, in3a);
            uint edge2 = graph.AddEdge(out2, in3b);

            graph.GetIncomingEdges(node3, out var edges, Allocator.Temp);

            Assert.AreEqual(2, edges.Length);
            Assert.Contains(edge1, edges.ToArray());
            Assert.Contains(edge2, edges.ToArray());

            edges.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetIncomingEdges_NodeWithNoIncomingEdges_ReturnsEmptyArray() {
            var graph = Graph.Create(Allocator.Temp);
            uint node = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.GetIncomingEdges(node, out var edges, Allocator.Temp);

            Assert.AreEqual(0, edges.Length);

            edges.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetSuccessorNodes_ReturnsConnectedNodes() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1a = graph.AddOutputPort(node1, portType: 1);
            uint out1b = graph.AddOutputPort(node1, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);
            uint in3 = graph.AddInputPort(node3, portType: 1);

            graph.AddEdge(out1a, in2);
            graph.AddEdge(out1b, in3);

            graph.GetSuccessorNodes(node1, out var successors, Allocator.Temp);

            Assert.AreEqual(2, successors.Length);
            Assert.Contains(node2, successors.ToArray());
            Assert.Contains(node3, successors.ToArray());

            successors.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetSuccessorNodes_NoOutgoingEdges_ReturnsEmpty() {
            var graph = Graph.Create(Allocator.Temp);
            uint node = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.GetSuccessorNodes(node, out var successors, Allocator.Temp);

            Assert.AreEqual(0, successors.Length);

            successors.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetSuccessorNodes_MultipleEdgesToSameNode_ReturnsOnce() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1a = graph.AddOutputPort(node1, portType: 1);
            uint out1b = graph.AddOutputPort(node1, portType: 1);
            uint in2a = graph.AddInputPort(node2, portType: 1);
            uint in2b = graph.AddInputPort(node2, portType: 1);

            graph.AddEdge(out1a, in2a);
            graph.AddEdge(out1b, in2b);

            graph.GetSuccessorNodes(node1, out var successors, Allocator.Temp);

            Assert.AreEqual(1, successors.Length);
            Assert.AreEqual(node2, successors[0]);

            successors.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetPredecessorNodes_ReturnsConnectedNodes() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint out2 = graph.AddOutputPort(node2, portType: 1);
            uint in3a = graph.AddInputPort(node3, portType: 1);
            uint in3b = graph.AddInputPort(node3, portType: 1);

            graph.AddEdge(out1, in3a);
            graph.AddEdge(out2, in3b);

            graph.GetPredecessorNodes(node3, out var predecessors, Allocator.Temp);

            Assert.AreEqual(2, predecessors.Length);
            Assert.Contains(node1, predecessors.ToArray());
            Assert.Contains(node2, predecessors.ToArray());

            predecessors.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetPredecessorNodes_NoIncomingEdges_ReturnsEmpty() {
            var graph = Graph.Create(Allocator.Temp);
            uint node = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.GetPredecessorNodes(node, out var predecessors, Allocator.Temp);

            Assert.AreEqual(0, predecessors.Length);

            predecessors.Dispose();
            graph.Dispose();
        }

        [Test]
        public void FindSourceNodes_ReturnsNodesWithNoIncomingEdges() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint out2 = graph.AddOutputPort(node2, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);
            uint in3 = graph.AddInputPort(node3, portType: 1);

            graph.AddEdge(out1, in2);
            graph.AddEdge(out2, in3);

            graph.FindSourceNodes(out var sources, Allocator.Temp);

            Assert.AreEqual(1, sources.Length);
            Assert.AreEqual(node1, sources[0]);

            sources.Dispose();
            graph.Dispose();
        }

        [Test]
        public void FindSourceNodes_AllNodesAreRoots_ReturnsAll() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.FindSourceNodes(out var sources, Allocator.Temp);

            Assert.AreEqual(2, sources.Length);
            Assert.Contains(node1, sources.ToArray());
            Assert.Contains(node2, sources.ToArray());

            sources.Dispose();
            graph.Dispose();
        }

        [Test]
        public void FindSourceNodes_EmptyGraph_ReturnsEmpty() {
            var graph = Graph.Create(Allocator.Temp);

            graph.FindSourceNodes(out var sources, Allocator.Temp);

            Assert.AreEqual(0, sources.Length);

            sources.Dispose();
            graph.Dispose();
        }

        [Test]
        public void FindSinkNodes_ReturnsNodesWithNoOutgoingEdges() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint out2 = graph.AddOutputPort(node2, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);
            uint in3 = graph.AddInputPort(node3, portType: 1);

            graph.AddEdge(out1, in2);
            graph.AddEdge(out2, in3);

            graph.FindSinkNodes(out var sinks, Allocator.Temp);

            Assert.AreEqual(1, sinks.Length);
            Assert.AreEqual(node3, sinks[0]);

            sinks.Dispose();
            graph.Dispose();
        }

        [Test]
        public void FindSinkNodes_AllNodesAreSinks_ReturnsAll() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.FindSinkNodes(out var sinks, Allocator.Temp);

            Assert.AreEqual(2, sinks.Length);
            Assert.Contains(node1, sinks.ToArray());
            Assert.Contains(node2, sinks.ToArray());

            sinks.Dispose();
            graph.Dispose();
        }
    }
}
