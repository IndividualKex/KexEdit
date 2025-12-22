using NUnit.Framework;
using Unity.Collections;

namespace KexEdit.Graph.Tests {
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
            graph.Dispose();
        }

        [Test]
        public void Graph_Dispose_FreesMemory() {
            var graph = Graph.Create(Allocator.Temp);

            graph.Dispose();

            Assert.IsFalse(graph.NodeIds.IsCreated);
            Assert.IsFalse(graph.NodeTypes.IsCreated);
            Assert.IsFalse(graph.NodePositions.IsCreated);
            graph.Dispose();
        }

        [Test]
        public void Graph_NodeCount_ReturnsCorrectValue() {
            var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(0, graph.NodeCount);
            graph.Dispose();
        }

        [Test]
        public void Graph_PortCount_ReturnsCorrectValue() {
            var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(0, graph.PortCount);
            graph.Dispose();
        }

        [Test]
        public void Graph_EdgeCount_ReturnsCorrectValue() {
            var graph = Graph.Create(Allocator.Temp);

            Assert.AreEqual(0, graph.EdgeCount);
            graph.Dispose();
        }
    }
}
