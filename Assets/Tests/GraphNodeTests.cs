using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace KexGraph.Tests {
    [TestFixture]
    [Category("Unit")]
    public class GraphNodeTests {
        [Test]
        public void AddNode_ValidData_IncreasesCount() {
            using var graph = Graph.Create(Allocator.Temp);

            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            Assert.AreEqual(1, graph.NodeCount);
            Assert.AreNotEqual(0u, nodeId);
        }

        [Test]
        public void AddNode_GeneratesUniqueIds() {
            using var graph = Graph.Create(Allocator.Temp);

            uint id1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint id2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint id3 = graph.AddNode(nodeType: 1, position: float2.zero);

            Assert.AreNotEqual(id1, id2);
            Assert.AreNotEqual(id2, id3);
            Assert.AreNotEqual(id1, id3);
        }

        [Test]
        public void TryGetNodeIndex_ExistingNode_ReturnsTrue() {
            using var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: new float2(10, 20));

            bool found = graph.TryGetNodeIndex(nodeId, out int index);

            Assert.IsTrue(found);
            Assert.AreEqual(0, index);
        }

        [Test]
        public void TryGetNodeIndex_NonexistentNode_ReturnsFalse() {
            using var graph = Graph.Create(Allocator.Temp);

            bool found = graph.TryGetNodeIndex(999u, out int index);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void AddNode_StoresCorrectPosition() {
            using var graph = Graph.Create(Allocator.Temp);
            var position = new float2(10, 20);

            uint nodeId = graph.AddNode(nodeType: 1, position: position);
            graph.TryGetNodeIndex(nodeId, out int index);

            Assert.AreEqual(position, graph.NodePositions[index]);
        }

        [Test]
        public void AddNode_StoresCorrectType() {
            using var graph = Graph.Create(Allocator.Temp);

            uint nodeId = graph.AddNode(nodeType: 42, position: float2.zero);
            graph.TryGetNodeIndex(nodeId, out int index);

            Assert.AreEqual(42u, graph.NodeTypes[index]);
        }

        [Test]
        public void RemoveNode_DecreasesCount() {
            using var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.RemoveNode(nodeId);

            Assert.AreEqual(0, graph.NodeCount);
        }

        [Test]
        public void RemoveNode_MakesNodeUnfindable() {
            using var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.RemoveNode(nodeId);

            bool found = graph.TryGetNodeIndex(nodeId, out _);
            Assert.IsFalse(found);
        }

        [Test]
        public void RemoveNode_MiddleNode_PreservesOtherNodes() {
            using var graph = Graph.Create(Allocator.Temp);
            uint id1 = graph.AddNode(nodeType: 1, position: new float2(1, 1));
            uint id2 = graph.AddNode(nodeType: 2, position: new float2(2, 2));
            uint id3 = graph.AddNode(nodeType: 3, position: new float2(3, 3));

            graph.RemoveNode(id2);

            Assert.AreEqual(2, graph.NodeCount);
            Assert.IsTrue(graph.TryGetNodeIndex(id1, out _));
            Assert.IsTrue(graph.TryGetNodeIndex(id3, out _));
            Assert.IsFalse(graph.TryGetNodeIndex(id2, out _));
        }
    }
}
