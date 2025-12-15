using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace KexGraph.Tests {
    [TestFixture]
    [Category("Unit")]
    public class GraphStructureTests {
        [Test]
        public void Graph_Create_AllocatesLists() {
            var graph = Graph.Create(Allocator.Temp);

            Assert.IsTrue(graph.NodeIds.IsCreated);
            Assert.IsTrue(graph.NodeTypes.IsCreated);
            Assert.IsTrue(graph.NodePositions.IsCreated);
            Assert.IsTrue(graph.PortIds.IsCreated);
            Assert.IsTrue(graph.EdgeIds.IsCreated);

            graph.Dispose();
        }

        [Test]
        public void Graph_Dispose_FreesMemory() {
            var graph = Graph.Create(Allocator.Temp);

            graph.Dispose();

            Assert.IsFalse(graph.NodeIds.IsCreated);
            Assert.IsFalse(graph.NodeTypes.IsCreated);
            Assert.IsFalse(graph.NodePositions.IsCreated);
        }

        [Test]
        public void Graph_NodeCount_ReturnsCorrectValue() {
            using var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(0, graph.NodeCount);
        }

        [Test]
        public void Graph_PortCount_ReturnsCorrectValue() {
            using var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(0, graph.PortCount);
        }

        [Test]
        public void Graph_EdgeCount_ReturnsCorrectValue() {
            using var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(0, graph.EdgeCount);
        }
    }
}
