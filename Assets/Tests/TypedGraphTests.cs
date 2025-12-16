using KexEdit.NodeGraph;
using KexEdit.Nodes;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace NodeGraph.Tests {
    [TestFixture]
    [Category("Unit")]
    public class TypedGraphTests {
        [Test]
        public void CreateNode_Force_CreatesCorrectPorts() {
            var graph = Graph.Create(Allocator.Temp);

            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            Assert.AreEqual(2, inputs.Length);
            Assert.AreEqual(2, outputs.Length);
            Assert.AreEqual(1, graph.NodeCount);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void CreateNode_Curved_CreatesSixInputs() {
            var graph = Graph.Create(Allocator.Temp);

            graph.CreateNode(NodeType.Curved, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            Assert.AreEqual(6, inputs.Length);
            Assert.AreEqual(2, outputs.Length);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void CreateNode_Anchor_CreatesOneOutput() {
            var graph = Graph.Create(Allocator.Temp);

            graph.CreateNode(NodeType.Anchor, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            Assert.AreEqual(2, inputs.Length);
            Assert.AreEqual(1, outputs.Length);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetNodeType_ReturnsCorrectType() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Bridge, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetNodeType(nodeId, out NodeType nodeType);

            Assert.IsTrue(found);
            Assert.AreEqual(NodeType.Bridge, nodeType);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetPortType_ReturnsCorrectType() {
            var graph = Graph.Create(Allocator.Temp);
            graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetPortType(inputs[0], out PortId portType);

            Assert.IsTrue(found);
            Assert.AreEqual(PortId.Anchor, portType);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetInputPort_FindsAnchorPort() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetInputPort(nodeId, PortId.Anchor, out uint portId);

            Assert.IsTrue(found);
            Assert.AreEqual(inputs[0], portId);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetInputPort_NonexistentType_ReturnsFalse() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetInputPort(nodeId, PortId.Radius, out _);

            Assert.IsFalse(found);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetOutputPort_FindsPathPort() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetOutputPort(nodeId, PortId.Path, out uint portId);

            Assert.IsTrue(found);
            Assert.AreEqual(outputs[1], portId);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void RemoveNodeCascade_RemovesNodeAndPorts() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            graph.RemoveNodeCascade(nodeId);

            Assert.AreEqual(0, graph.NodeCount);
            Assert.AreEqual(0, graph.PortCount);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void RemoveNodeCascade_RemovesConnectedEdges() {
            var graph = Graph.Create(Allocator.Temp);

            uint anchorId = graph.CreateNode(NodeType.Anchor, float2.zero,
                out var anchorIn, out var anchorOut, Allocator.Temp);
            uint forceId = graph.CreateNode(NodeType.Force, new float2(100, 0),
                out var forceIn, out var forceOut, Allocator.Temp);

            graph.AddEdge(anchorOut[0], forceIn[0]);
            Assert.AreEqual(1, graph.EdgeCount);

            graph.RemoveNodeCascade(anchorId);

            Assert.AreEqual(0, graph.EdgeCount);

            anchorIn.Dispose();
            anchorOut.Dispose();
            forceIn.Dispose();
            forceOut.Dispose();
            graph.Dispose();
        }
    }

    [TestFixture]
    [Category("Unit")]
    public class ConnectionValidatorTests {
        [Test]
        public void ValidateConnection_CompatiblePorts_ReturnsSuccess() {
            var graph = Graph.Create(Allocator.Temp);

            uint anchorId = graph.CreateNode(NodeType.Anchor, float2.zero,
                out var anchorIn, out var anchorOut, Allocator.Temp);
            uint forceId = graph.CreateNode(NodeType.Force, float2.zero,
                out var forceIn, out var forceOut, Allocator.Temp);

            bool valid = graph.ValidateConnection(anchorOut[0], forceIn[0], out var error);

            Assert.IsTrue(valid);

            anchorIn.Dispose();
            anchorOut.Dispose();
            forceIn.Dispose();
            forceOut.Dispose();
            graph.Dispose();
        }

        [Test]
        public void ValidateConnection_IncompatibleTypes_ReturnsError() {
            var graph = Graph.Create(Allocator.Temp);

            uint anchorId = graph.CreateNode(NodeType.Anchor, float2.zero,
                out var anchorIn, out var anchorOut, Allocator.Temp);
            uint forceId = graph.CreateNode(NodeType.Force, float2.zero,
                out var forceIn, out var forceOut, Allocator.Temp);

            // Anchor output (PortId.Anchor) to Duration input (PortId.Duration)
            bool valid = graph.ValidateConnection(anchorOut[0], forceIn[1], out var error);

            Assert.IsFalse(valid);
            Assert.AreEqual(ValidationError.IncompatiblePortTypes, error);

            anchorIn.Dispose();
            anchorOut.Dispose();
            forceIn.Dispose();
            forceOut.Dispose();
            graph.Dispose();
        }

        [Test]
        public void ValidateConnection_OutputToOutput_ReturnsError() {
            var graph = Graph.Create(Allocator.Temp);

            uint node1 = graph.CreateNode(NodeType.Force, float2.zero,
                out var in1, out var out1, Allocator.Temp);
            uint node2 = graph.CreateNode(NodeType.Force, float2.zero,
                out var in2, out var out2, Allocator.Temp);

            bool valid = graph.ValidateConnection(out1[0], out2[0], out var error);

            Assert.IsFalse(valid);
            Assert.AreEqual(ValidationError.TargetMustBeInput, error);

            in1.Dispose();
            out1.Dispose();
            in2.Dispose();
            out2.Dispose();
            graph.Dispose();
        }

        [Test]
        public void ValidateConnection_InputToOutput_ReturnsError() {
            var graph = Graph.Create(Allocator.Temp);

            uint node1 = graph.CreateNode(NodeType.Force, float2.zero,
                out var in1, out var out1, Allocator.Temp);
            uint node2 = graph.CreateNode(NodeType.Force, float2.zero,
                out var in2, out var out2, Allocator.Temp);

            bool valid = graph.ValidateConnection(in1[0], out2[0], out var error);

            Assert.IsFalse(valid);
            Assert.AreEqual(ValidationError.SourceMustBeOutput, error);

            in1.Dispose();
            out1.Dispose();
            in2.Dispose();
            out2.Dispose();
            graph.Dispose();
        }

        [Test]
        public void ValidateConnection_SelfConnection_ReturnsError() {
            var graph = Graph.Create(Allocator.Temp);

            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool valid = graph.ValidateConnection(outputs[0], inputs[0], out var error);

            Assert.IsFalse(valid);
            Assert.AreEqual(ValidationError.SelfConnection, error);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void AddValidatedEdge_ValidConnection_CreatesEdge() {
            var graph = Graph.Create(Allocator.Temp);

            uint anchorId = graph.CreateNode(NodeType.Anchor, float2.zero,
                out var anchorIn, out var anchorOut, Allocator.Temp);
            uint forceId = graph.CreateNode(NodeType.Force, float2.zero,
                out var forceIn, out var forceOut, Allocator.Temp);

            uint edgeId = graph.AddValidatedEdge(anchorOut[0], forceIn[0], out var error);

            Assert.AreEqual(ValidationError.None, error);
            Assert.AreNotEqual(0u, edgeId);
            Assert.AreEqual(1, graph.EdgeCount);

            anchorIn.Dispose();
            anchorOut.Dispose();
            forceIn.Dispose();
            forceOut.Dispose();
            graph.Dispose();
        }

        [Test]
        public void AddValidatedEdge_InvalidConnection_ReturnsZero() {
            var graph = Graph.Create(Allocator.Temp);

            uint anchorId = graph.CreateNode(NodeType.Anchor, float2.zero,
                out var anchorIn, out var anchorOut, Allocator.Temp);
            uint forceId = graph.CreateNode(NodeType.Force, float2.zero,
                out var forceIn, out var forceOut, Allocator.Temp);

            uint edgeId = graph.AddValidatedEdge(anchorOut[0], forceIn[1], out var error);

            Assert.AreNotEqual(ValidationError.None, error);
            Assert.AreEqual(0u, edgeId);
            Assert.AreEqual(0, graph.EdgeCount);

            anchorIn.Dispose();
            anchorOut.Dispose();
            forceIn.Dispose();
            forceOut.Dispose();
            graph.Dispose();
        }

        [Test]
        public void ValidateAllEdges_ValidGraph_ReturnsTrue() {
            var graph = Graph.Create(Allocator.Temp);

            uint anchorId = graph.CreateNode(NodeType.Anchor, float2.zero,
                out var anchorIn, out var anchorOut, Allocator.Temp);
            uint forceId = graph.CreateNode(NodeType.Force, float2.zero,
                out var forceIn, out var forceOut, Allocator.Temp);

            graph.AddValidatedEdge(anchorOut[0], forceIn[0], out _);

            bool valid = graph.ValidateAllEdges(out int invalidIndex);

            Assert.IsTrue(valid);
            Assert.AreEqual(-1, invalidIndex);

            anchorIn.Dispose();
            anchorOut.Dispose();
            forceIn.Dispose();
            forceOut.Dispose();
            graph.Dispose();
        }
    }

    [TestFixture]
    [Category("Unit")]
    public class PortDataTypeTests {
        [Test]
        public void DataType_AnchorPort_ReturnsAnchor() {
            Assert.AreEqual(PortDataType.Anchor, PortId.Anchor.DataType());
        }

        [Test]
        public void DataType_PathPort_ReturnsPath() {
            Assert.AreEqual(PortDataType.Path, PortId.Path.DataType());
        }

        [Test]
        public void DataType_ScalarPorts_ReturnScalar() {
            Assert.AreEqual(PortDataType.Scalar, PortId.Duration.DataType());
            Assert.AreEqual(PortDataType.Scalar, PortId.Radius.DataType());
            Assert.AreEqual(PortDataType.Scalar, PortId.Arc.DataType());
        }

        [Test]
        public void DataType_VectorPorts_ReturnVector() {
            Assert.AreEqual(PortDataType.Vector, PortId.Position.DataType());
            Assert.AreEqual(PortDataType.Vector, PortId.Rotation.DataType());
        }

        [Test]
        public void DefaultValue_Duration_Returns5() {
            Assert.AreEqual(5f, PortId.Duration.DefaultValue());
        }

        [Test]
        public void DefaultValue_Weights_Return0Point5() {
            Assert.AreEqual(0.5f, PortId.InWeight.DefaultValue());
            Assert.AreEqual(0.5f, PortId.OutWeight.DefaultValue());
        }

        [Test]
        public void DefaultValue_StartEnd_Return0And1() {
            Assert.AreEqual(0f, PortId.Start.DefaultValue());
            Assert.AreEqual(1f, PortId.End.DefaultValue());
        }
    }
}
