using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace KexGraph.Tests {
    [TestFixture]
    [Category("Unit")]
    public class GraphPortTests {
        [Test]
        public void AddInputPort_ValidNode_IncreasesPortCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            uint portId = graph.AddInputPort(nodeId, portType: 1);

            Assert.AreEqual(1, graph.PortCount);
            Assert.AreNotEqual(0u, portId);
            graph.Dispose();
        }

        [Test]
        public void AddInputPort_GeneratesUniqueIds() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            uint id1 = graph.AddInputPort(nodeId, portType: 1);
            uint id2 = graph.AddInputPort(nodeId, portType: 1);
            uint id3 = graph.AddInputPort(nodeId, portType: 1);

            Assert.AreNotEqual(id1, id2);
            Assert.AreNotEqual(id2, id3);
            Assert.AreNotEqual(id1, id3);
            graph.Dispose();
        }

        [Test]
        public void AddInputPort_StoresCorrectPortType() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            uint portId = graph.AddInputPort(nodeId, portType: 42);
            graph.TryGetPortIndex(portId, out int index);

            Assert.AreEqual(42u, graph.PortTypes[index]);
            graph.Dispose();
        }

        [Test]
        public void AddInputPort_StoresCorrectOwner() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            uint portId = graph.AddInputPort(nodeId, portType: 1);
            graph.TryGetPortIndex(portId, out int index);

            Assert.AreEqual(nodeId, graph.PortOwners[index]);
            graph.Dispose();
        }

        [Test]
        public void AddInputPort_MarksAsInput() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            uint portId = graph.AddInputPort(nodeId, portType: 1);
            graph.TryGetPortIndex(portId, out int index);

            Assert.IsTrue(graph.PortIsInput[index]);
            graph.Dispose();
        }

        [Test]
        public void AddInputPort_UpdatesNodeInputCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.AddInputPort(nodeId, portType: 1);
            graph.AddInputPort(nodeId, portType: 1);
            graph.TryGetNodeIndex(nodeId, out int nodeIndex);

            Assert.AreEqual(2, graph.NodeInputCount[nodeIndex]);
            graph.Dispose();
        }

        [Test]
        public void AddOutputPort_ValidNode_IncreasesPortCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            uint portId = graph.AddOutputPort(nodeId, portType: 1);

            Assert.AreEqual(1, graph.PortCount);
            Assert.AreNotEqual(0u, portId);
            graph.Dispose();
        }

        [Test]
        public void AddOutputPort_MarksAsOutput() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            uint portId = graph.AddOutputPort(nodeId, portType: 1);
            graph.TryGetPortIndex(portId, out int index);

            Assert.IsFalse(graph.PortIsInput[index]);
            graph.Dispose();
        }

        [Test]
        public void AddOutputPort_UpdatesNodeOutputCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.AddOutputPort(nodeId, portType: 1);
            graph.AddOutputPort(nodeId, portType: 1);
            graph.TryGetNodeIndex(nodeId, out int nodeIndex);

            Assert.AreEqual(2, graph.NodeOutputCount[nodeIndex]);
            graph.Dispose();
        }

        [Test]
        public void GetInputPorts_NodeWithInputs_ReturnsCorrectPorts() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);
            uint port1 = graph.AddInputPort(nodeId, portType: 1);
            uint port2 = graph.AddInputPort(nodeId, portType: 2);

            graph.GetInputPorts(nodeId, out var ports, Allocator.Temp);

            Assert.AreEqual(2, ports.Length);
            Assert.Contains(port1, ports.ToArray());
            Assert.Contains(port2, ports.ToArray());

            ports.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetInputPorts_NodeWithNoInputs_ReturnsEmptyArray() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.GetInputPorts(nodeId, out var ports, Allocator.Temp);

            Assert.AreEqual(0, ports.Length);

            ports.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetOutputPorts_NodeWithOutputs_ReturnsCorrectPorts() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);
            uint port1 = graph.AddOutputPort(nodeId, portType: 1);
            uint port2 = graph.AddOutputPort(nodeId, portType: 2);

            graph.GetOutputPorts(nodeId, out var ports, Allocator.Temp);

            Assert.AreEqual(2, ports.Length);
            Assert.Contains(port1, ports.ToArray());
            Assert.Contains(port2, ports.ToArray());

            ports.Dispose();
            graph.Dispose();
        }

        [Test]
        public void GetOutputPorts_NodeWithNoOutputs_ReturnsEmptyArray() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);

            graph.GetOutputPorts(nodeId, out var ports, Allocator.Temp);

            Assert.AreEqual(0, ports.Length);

            ports.Dispose();
            graph.Dispose();
        }

        [Test]
        public void RemovePort_DecreasesPortCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);
            uint portId = graph.AddInputPort(nodeId, portType: 1);

            graph.RemovePort(portId);

            Assert.AreEqual(0, graph.PortCount);
            graph.Dispose();
        }

        [Test]
        public void RemovePort_UpdatesNodeInputCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);
            uint port1 = graph.AddInputPort(nodeId, portType: 1);
            uint port2 = graph.AddInputPort(nodeId, portType: 1);

            graph.RemovePort(port1);
            graph.TryGetNodeIndex(nodeId, out int nodeIndex);

            Assert.AreEqual(1, graph.NodeInputCount[nodeIndex]);
            graph.Dispose();
        }

        [Test]
        public void RemovePort_UpdatesNodeOutputCount() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.AddNode(nodeType: 1, position: float2.zero);
            uint port1 = graph.AddOutputPort(nodeId, portType: 1);
            uint port2 = graph.AddOutputPort(nodeId, portType: 1);

            graph.RemovePort(port1);
            graph.TryGetNodeIndex(nodeId, out int nodeIndex);

            Assert.AreEqual(1, graph.NodeOutputCount[nodeIndex]);
            graph.Dispose();
        }
    }
}
