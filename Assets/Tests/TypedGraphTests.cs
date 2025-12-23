using KexEdit.Graph.Typed;
using KexEdit.Sim.Schema;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace NodeGraph.Tests {
    [TestFixture]
    [Category("Unit")]
    public class PortSpecLookupTests {
        [Test]
        public void TryGetInputBySpec_FindsCorrectPort() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Curved, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            // Find the Arc port (Scalar index 1)
            bool found = graph.TryGetInputBySpec(nodeId, PortDataType.Scalar, 1, out uint portId);

            Assert.IsTrue(found);
            Assert.AreEqual(inputs[2], portId); // Arc is at index 2 (after Anchor, Radius)

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetInputBySpec_Bridge_FindsBothAnchors() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Bridge, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool foundFirst = graph.TryGetInputBySpec(nodeId, PortDataType.Anchor, 0, out uint firstAnchor);
            bool foundSecond = graph.TryGetInputBySpec(nodeId, PortDataType.Anchor, 1, out uint secondAnchor);

            Assert.IsTrue(foundFirst);
            Assert.IsTrue(foundSecond);
            Assert.AreEqual(inputs[0], firstAnchor);
            Assert.AreEqual(inputs[1], secondAnchor);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetInputBySpec_NonexistentIndex_ReturnsFalse() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            // Force only has 1 scalar (Duration at index 0)
            bool found = graph.TryGetInputBySpec(nodeId, PortDataType.Scalar, 5, out _);

            Assert.IsFalse(found);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetOutputBySpec_FindsPathPort() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetOutputBySpec(nodeId, PortDataType.Path, 0, out uint portId);

            Assert.IsTrue(found);
            Assert.AreEqual(outputs[1], portId);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetInput_FindsCorrectPort() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Curved, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            // Find Arc port by index 2
            bool found = graph.TryGetInput(nodeId, 2, out uint portId);

            Assert.IsTrue(found);
            Assert.AreEqual(inputs[2], portId);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetInput_InvalidIndex_ReturnsFalse() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetInput(nodeId, 99, out _);

            Assert.IsFalse(found);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void TryGetOutput_FindsCorrectPort() {
            var graph = Graph.Create(Allocator.Temp);
            uint nodeId = graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetOutput(nodeId, 1, out uint portId);

            Assert.IsTrue(found);
            Assert.AreEqual(outputs[1], portId);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }
    }
}

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

            Assert.AreEqual(8, inputs.Length);
            Assert.AreEqual(1, outputs.Length);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void CreateNode_Scalar_CreatesNoInputsOneOutput() {
            var graph = Graph.Create(Allocator.Temp);

            graph.CreateNode(NodeType.Scalar, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            Assert.AreEqual(0, inputs.Length);
            Assert.AreEqual(1, outputs.Length);

            inputs.Dispose();
            outputs.Dispose();
            graph.Dispose();
        }

        [Test]
        public void CreateNode_Vector_CreatesNoInputsOneOutput() {
            var graph = Graph.Create(Allocator.Temp);

            graph.CreateNode(NodeType.Vector, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            Assert.AreEqual(0, inputs.Length);
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
        public void TryGetPortSpec_ReturnsCorrectSpec() {
            var graph = Graph.Create(Allocator.Temp);
            graph.CreateNode(NodeType.Force, float2.zero,
                out var inputs, out var outputs, Allocator.Temp);

            bool found = graph.TryGetPortSpec(inputs[0], out PortSpec portSpec);

            Assert.IsTrue(found);
            Assert.AreEqual(PortDataType.Anchor, portSpec.DataType);
            Assert.AreEqual(0, portSpec.LocalIndex);

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

            // Anchor output to Duration input - incompatible types
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

        [Test]
        public void ValidateConnection_ScalarToScalarInput_ReturnsSuccess() {
            var graph = Graph.Create(Allocator.Temp);

            graph.CreateNode(NodeType.Scalar, float2.zero,
                out var scalarIn, out var scalarOut, Allocator.Temp);
            graph.CreateNode(NodeType.Force, float2.zero,
                out var forceIn, out var forceOut, Allocator.Temp);

            // Scalar output to Duration input - should succeed (both Scalar type)
            bool valid = graph.ValidateConnection(scalarOut[0], forceIn[1], out var error);

            Assert.IsTrue(valid);
            Assert.AreEqual(ValidationError.None, error);

            scalarIn.Dispose();
            scalarOut.Dispose();
            forceIn.Dispose();
            forceOut.Dispose();
            graph.Dispose();
        }

        [Test]
        public void ValidateConnection_VectorToPositionInput_ReturnsSuccess() {
            var graph = Graph.Create(Allocator.Temp);

            graph.CreateNode(NodeType.Vector, float2.zero,
                out var vectorIn, out var vectorOut, Allocator.Temp);
            graph.CreateNode(NodeType.Anchor, float2.zero,
                out var anchorIn, out var anchorOut, Allocator.Temp);

            // Vector output to Position input - should succeed (both Vector type)
            bool valid = graph.ValidateConnection(vectorOut[0], anchorIn[0], out var error);

            Assert.IsTrue(valid);
            Assert.AreEqual(ValidationError.None, error);

            vectorIn.Dispose();
            vectorOut.Dispose();
            anchorIn.Dispose();
            anchorOut.Dispose();
            graph.Dispose();
        }

        [Test]
        public void ValidateConnection_ScalarToVectorInput_ReturnsError() {
            var graph = Graph.Create(Allocator.Temp);

            graph.CreateNode(NodeType.Scalar, float2.zero,
                out var scalarIn, out var scalarOut, Allocator.Temp);
            graph.CreateNode(NodeType.Anchor, float2.zero,
                out var anchorIn, out var anchorOut, Allocator.Temp);

            // Scalar output to Position input - should fail (different types)
            bool valid = graph.ValidateConnection(scalarOut[0], anchorIn[0], out var error);

            Assert.IsFalse(valid);
            Assert.AreEqual(ValidationError.IncompatiblePortTypes, error);

            scalarIn.Dispose();
            scalarOut.Dispose();
            anchorIn.Dispose();
            anchorOut.Dispose();
            graph.Dispose();
        }
    }
}
