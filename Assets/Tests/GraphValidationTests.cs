using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Graph.Tests {
    [TestFixture]
    [Category("Unit")]
    public class GraphValidationTests {
        [Test]
        public void HasCycle_EmptyGraph_ReturnsFalse() {
            var graph = Graph.Create(Allocator.Temp);

            bool hasCycle = graph.HasCycle();

            Assert.IsFalse(hasCycle);
            graph.Dispose();
        }

        [Test]
        public void HasCycle_SingleNode_ReturnsFalse() {
            var graph = Graph.Create(Allocator.Temp);
            graph.AddNode(nodeType: 1, position: float2.zero);

            bool hasCycle = graph.HasCycle();

            Assert.IsFalse(hasCycle);
            graph.Dispose();
        }

        [Test]
        public void HasCycle_LinearChain_ReturnsFalse() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);

            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);
            uint out2 = graph.AddOutputPort(node2, portType: 1);
            uint in3 = graph.AddInputPort(node3, portType: 1);

            graph.AddEdge(out1, in2);
            graph.AddEdge(out2, in3);

            bool hasCycle = graph.HasCycle();

            Assert.IsFalse(hasCycle);
            graph.Dispose();
        }

        [Test]
        public void HasCycle_SimpleCycle_ReturnsTrue() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);

            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint in1 = graph.AddInputPort(node1, portType: 1);
            uint out2 = graph.AddOutputPort(node2, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);

            graph.AddEdge(out1, in2);
            graph.AddEdge(out2, in1);

            bool hasCycle = graph.HasCycle();

            Assert.IsTrue(hasCycle);
            graph.Dispose();
        }

        [Test]
        public void HasCycle_SelfLoop_ReturnsTrue() {
            var graph = Graph.Create(Allocator.Temp);
            uint node = graph.AddNode(nodeType: 1, position: float2.zero);

            uint output = graph.AddOutputPort(node, portType: 1);
            uint input = graph.AddInputPort(node, portType: 1);

            graph.AddEdge(output, input);

            bool hasCycle = graph.HasCycle();

            Assert.IsTrue(hasCycle);
            graph.Dispose();
        }

        [Test]
        public void HasCycle_ComplexCycle_ReturnsTrue() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node4 = graph.AddNode(nodeType: 1, position: float2.zero);

            uint out1 = graph.AddOutputPort(node1, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);
            uint out2 = graph.AddOutputPort(node2, portType: 1);
            uint in3 = graph.AddInputPort(node3, portType: 1);
            uint out3 = graph.AddOutputPort(node3, portType: 1);
            uint in4 = graph.AddInputPort(node4, portType: 1);
            uint out4 = graph.AddOutputPort(node4, portType: 1);
            uint in1 = graph.AddInputPort(node1, portType: 1);

            graph.AddEdge(out1, in2);
            graph.AddEdge(out2, in3);
            graph.AddEdge(out3, in4);
            graph.AddEdge(out4, in1);

            bool hasCycle = graph.HasCycle();

            Assert.IsTrue(hasCycle);
            graph.Dispose();
        }

        [Test]
        public void HasCycle_DiamondShape_ReturnsFalse() {
            var graph = Graph.Create(Allocator.Temp);
            uint node1 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node2 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node3 = graph.AddNode(nodeType: 1, position: float2.zero);
            uint node4 = graph.AddNode(nodeType: 1, position: float2.zero);

            uint out1a = graph.AddOutputPort(node1, portType: 1);
            uint out1b = graph.AddOutputPort(node1, portType: 1);
            uint in2 = graph.AddInputPort(node2, portType: 1);
            uint in3 = graph.AddInputPort(node3, portType: 1);
            uint out2 = graph.AddOutputPort(node2, portType: 1);
            uint out3 = graph.AddOutputPort(node3, portType: 1);
            uint in4a = graph.AddInputPort(node4, portType: 1);
            uint in4b = graph.AddInputPort(node4, portType: 1);

            graph.AddEdge(out1a, in2);
            graph.AddEdge(out1b, in3);
            graph.AddEdge(out2, in4a);
            graph.AddEdge(out3, in4b);

            bool hasCycle = graph.HasCycle();

            Assert.IsFalse(hasCycle);
            graph.Dispose();
        }
    }
}
