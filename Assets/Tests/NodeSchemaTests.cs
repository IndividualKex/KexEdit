using KexEdit.Nodes;
using NUnit.Framework;
using System;

namespace Tests {
    [TestFixture]
    [Category("Schema")]
    public class NodeSchemaTests {
        private static readonly NodeType[] AllNodeTypes = (NodeType[])Enum.GetValues(typeof(NodeType));

        [TestCase(NodeType.Scalar, 0)]
        [TestCase(NodeType.Vector, 0)]
        [TestCase(NodeType.Force, 2)]
        [TestCase(NodeType.Geometric, 2)]
        [TestCase(NodeType.Curved, 6)]
        [TestCase(NodeType.CopyPath, 4)]
        [TestCase(NodeType.Bridge, 3)]
        [TestCase(NodeType.Anchor, 2)]
        [TestCase(NodeType.Reverse, 1)]
        [TestCase(NodeType.ReversePath, 1)]
        public void InputCount_AllNodeTypes_ReturnsExpectedCount(NodeType type, int expected) {
            int actual = NodeSchema.InputCount(type);
            Assert.AreEqual(expected, actual, $"{type} input count mismatch");
        }

        [TestCase(NodeType.Scalar, 1)]
        [TestCase(NodeType.Vector, 1)]
        [TestCase(NodeType.Force, 2)]
        [TestCase(NodeType.Geometric, 2)]
        [TestCase(NodeType.Curved, 2)]
        [TestCase(NodeType.CopyPath, 2)]
        [TestCase(NodeType.Bridge, 2)]
        [TestCase(NodeType.Anchor, 1)]
        [TestCase(NodeType.Reverse, 1)]
        [TestCase(NodeType.ReversePath, 1)]
        public void OutputCount_AllNodeTypes_ReturnsExpectedCount(NodeType type, int expected) {
            int actual = NodeSchema.OutputCount(type);
            Assert.AreEqual(expected, actual, $"{type} output count mismatch");
        }

        [TestCase(NodeType.Scalar, 0)]
        [TestCase(NodeType.Vector, 0)]
        [TestCase(NodeType.Force, 7)]
        [TestCase(NodeType.Geometric, 7)]
        [TestCase(NodeType.Curved, 5)]
        [TestCase(NodeType.CopyPath, 4)]
        [TestCase(NodeType.Bridge, 5)]
        [TestCase(NodeType.Anchor, 0)]
        [TestCase(NodeType.Reverse, 0)]
        [TestCase(NodeType.ReversePath, 0)]
        public void PropertyCount_AllNodeTypes_ReturnsExpectedCount(NodeType type, int expected) {
            int actual = NodeSchema.PropertyCount(type);
            Assert.AreEqual(expected, actual, $"{type} property count mismatch");
        }

        [Test]
        public void Input_Force_ReturnsAnchorAndDuration() {
            Assert.AreEqual(PortId.Anchor, NodeSchema.Input(NodeType.Force, 0));
            Assert.AreEqual(PortId.Duration, NodeSchema.Input(NodeType.Force, 1));
        }

        [Test]
        public void Input_Geometric_ReturnsAnchorAndDuration() {
            Assert.AreEqual(PortId.Anchor, NodeSchema.Input(NodeType.Geometric, 0));
            Assert.AreEqual(PortId.Duration, NodeSchema.Input(NodeType.Geometric, 1));
        }

        [Test]
        public void Input_Curved_ReturnsSixPorts() {
            Assert.AreEqual(PortId.Anchor, NodeSchema.Input(NodeType.Curved, 0));
            Assert.AreEqual(PortId.Radius, NodeSchema.Input(NodeType.Curved, 1));
            Assert.AreEqual(PortId.Arc, NodeSchema.Input(NodeType.Curved, 2));
            Assert.AreEqual(PortId.Axis, NodeSchema.Input(NodeType.Curved, 3));
            Assert.AreEqual(PortId.LeadIn, NodeSchema.Input(NodeType.Curved, 4));
            Assert.AreEqual(PortId.LeadOut, NodeSchema.Input(NodeType.Curved, 5));
        }

        [Test]
        public void Input_CopyPath_ReturnsFourPorts() {
            Assert.AreEqual(PortId.Anchor, NodeSchema.Input(NodeType.CopyPath, 0));
            Assert.AreEqual(PortId.Path, NodeSchema.Input(NodeType.CopyPath, 1));
            Assert.AreEqual(PortId.Start, NodeSchema.Input(NodeType.CopyPath, 2));
            Assert.AreEqual(PortId.End, NodeSchema.Input(NodeType.CopyPath, 3));
        }

        [Test]
        public void Input_Bridge_ReturnsThreePorts() {
            Assert.AreEqual(PortId.Anchor, NodeSchema.Input(NodeType.Bridge, 0));
            Assert.AreEqual(PortId.InWeight, NodeSchema.Input(NodeType.Bridge, 1));
            Assert.AreEqual(PortId.OutWeight, NodeSchema.Input(NodeType.Bridge, 2));
        }

        [Test]
        public void Input_Anchor_ReturnsPositionAndRotation() {
            Assert.AreEqual(PortId.Position, NodeSchema.Input(NodeType.Anchor, 0));
            Assert.AreEqual(PortId.Rotation, NodeSchema.Input(NodeType.Anchor, 1));
        }

        [Test]
        public void Output_Force_ReturnsAnchorAndPath() {
            Assert.AreEqual(PortId.Anchor, NodeSchema.Output(NodeType.Force, 0));
            Assert.AreEqual(PortId.Path, NodeSchema.Output(NodeType.Force, 1));
        }

        [Test]
        public void Output_Anchor_ReturnsAnchor() {
            Assert.AreEqual(PortId.Anchor, NodeSchema.Output(NodeType.Anchor, 0));
        }

        [Test]
        public void Input_InvalidIndex_Returns255() {
            PortId result = NodeSchema.Input(NodeType.Force, 99);
            Assert.AreEqual((PortId)255, result);
        }

        [Test]
        public void Output_InvalidIndex_Returns255() {
            PortId result = NodeSchema.Output(NodeType.Force, 99);
            Assert.AreEqual((PortId)255, result);
        }

        [Test]
        public void Property_InvalidIndex_Returns255() {
            PropertyId result = NodeSchema.Property(NodeType.Force, 99);
            Assert.AreEqual((PropertyId)255, result);
        }

        [Test]
        public void Property_Force_ReturnsSevenProperties() {
            Assert.AreEqual(PropertyId.RollSpeed, NodeSchema.Property(NodeType.Force, 0));
            Assert.AreEqual(PropertyId.NormalForce, NodeSchema.Property(NodeType.Force, 1));
            Assert.AreEqual(PropertyId.LateralForce, NodeSchema.Property(NodeType.Force, 2));
            Assert.AreEqual(PropertyId.DrivenVelocity, NodeSchema.Property(NodeType.Force, 3));
            Assert.AreEqual(PropertyId.HeartOffset, NodeSchema.Property(NodeType.Force, 4));
            Assert.AreEqual(PropertyId.Friction, NodeSchema.Property(NodeType.Force, 5));
            Assert.AreEqual(PropertyId.Resistance, NodeSchema.Property(NodeType.Force, 6));
        }

        [Test]
        public void Property_Geometric_ReturnsSevenProperties() {
            Assert.AreEqual(PropertyId.RollSpeed, NodeSchema.Property(NodeType.Geometric, 0));
            Assert.AreEqual(PropertyId.PitchSpeed, NodeSchema.Property(NodeType.Geometric, 1));
            Assert.AreEqual(PropertyId.YawSpeed, NodeSchema.Property(NodeType.Geometric, 2));
            Assert.AreEqual(PropertyId.DrivenVelocity, NodeSchema.Property(NodeType.Geometric, 3));
            Assert.AreEqual(PropertyId.HeartOffset, NodeSchema.Property(NodeType.Geometric, 4));
            Assert.AreEqual(PropertyId.Friction, NodeSchema.Property(NodeType.Geometric, 5));
            Assert.AreEqual(PropertyId.Resistance, NodeSchema.Property(NodeType.Geometric, 6));
        }

        [Test]
        public void Property_Force_And_Geometric_ShareRollSpeedAtIndex0() {
            Assert.AreEqual(NodeSchema.Property(NodeType.Force, 0), NodeSchema.Property(NodeType.Geometric, 0));
            Assert.AreEqual(PropertyId.RollSpeed, NodeSchema.Property(NodeType.Force, 0));
        }

        [Test]
        public void Property_Curved_ReturnsFiveProperties() {
            Assert.AreEqual(PropertyId.RollSpeed, NodeSchema.Property(NodeType.Curved, 0));
            Assert.AreEqual(PropertyId.DrivenVelocity, NodeSchema.Property(NodeType.Curved, 1));
            Assert.AreEqual(PropertyId.HeartOffset, NodeSchema.Property(NodeType.Curved, 2));
            Assert.AreEqual(PropertyId.Friction, NodeSchema.Property(NodeType.Curved, 3));
            Assert.AreEqual(PropertyId.Resistance, NodeSchema.Property(NodeType.Curved, 4));
        }

        [Test]
        public void Property_Bridge_IncludesTrackStyle() {
            Assert.AreEqual(PropertyId.TrackStyle, NodeSchema.Property(NodeType.Bridge, 4));
        }

        [Test]
        public void Property_Anchor_HasNoProperties() {
            Assert.AreEqual(0, NodeSchema.PropertyCount(NodeType.Anchor));
        }

        [Test]
        public void AllNodeTypes_HaveValidInputs() {
            foreach (NodeType type in AllNodeTypes) {
                int count = NodeSchema.InputCount(type);
                for (int i = 0; i < count; i++) {
                    PortId port = NodeSchema.Input(type, i);
                    Assert.AreNotEqual((PortId)255, port, $"{type} input {i} should be valid");
                }
            }
        }

        [Test]
        public void AllNodeTypes_HaveValidOutputs() {
            foreach (NodeType type in AllNodeTypes) {
                int count = NodeSchema.OutputCount(type);
                for (int i = 0; i < count; i++) {
                    PortId port = NodeSchema.Output(type, i);
                    Assert.AreNotEqual((PortId)255, port, $"{type} output {i} should be valid");
                }
            }
        }

        [Test]
        public void AllNodeTypes_HaveValidProperties() {
            foreach (NodeType type in AllNodeTypes) {
                int count = NodeSchema.PropertyCount(type);
                for (int i = 0; i < count; i++) {
                    PropertyId prop = NodeSchema.Property(type, i);
                    Assert.AreNotEqual((PropertyId)255, prop, $"{type} property {i} should be valid");
                }
            }
        }

        [Test]
        public void AllNodeTypes_InputIndexesAreContiguous() {
            foreach (NodeType type in AllNodeTypes) {
                int count = NodeSchema.InputCount(type);
                for (int i = 0; i < count; i++) {
                    PortId port = NodeSchema.Input(type, i);
                    Assert.AreNotEqual((PortId)255, port, $"{type} missing input at index {i}");
                }
                if (count > 0) {
                    PortId invalid = NodeSchema.Input(type, count);
                    Assert.AreEqual((PortId)255, invalid, $"{type} should return 255 at index {count}");
                }
            }
        }

        [Test]
        public void AllNodeTypes_OutputIndexesAreContiguous() {
            foreach (NodeType type in AllNodeTypes) {
                int count = NodeSchema.OutputCount(type);
                for (int i = 0; i < count; i++) {
                    PortId port = NodeSchema.Output(type, i);
                    Assert.AreNotEqual((PortId)255, port, $"{type} missing output at index {i}");
                }
                if (count > 0) {
                    PortId invalid = NodeSchema.Output(type, count);
                    Assert.AreEqual((PortId)255, invalid, $"{type} should return 255 at index {count}");
                }
            }
        }

        [Test]
        public void AllNodeTypes_PropertyIndexesAreContiguous() {
            foreach (NodeType type in AllNodeTypes) {
                int count = NodeSchema.PropertyCount(type);
                for (int i = 0; i < count; i++) {
                    PropertyId prop = NodeSchema.Property(type, i);
                    Assert.AreNotEqual((PropertyId)255, prop, $"{type} missing property at index {i}");
                }
                if (count > 0) {
                    PropertyId invalid = NodeSchema.Property(type, count);
                    Assert.AreEqual((PropertyId)255, invalid, $"{type} should return 255 at index {count}");
                }
            }
        }
    }

    [TestFixture]
    [Category("Schema")]
    public class PropertyIndexTests {
        [Test]
        public void ForceNode_PropertyIndexRoundTrip() {
            var properties = new[] {
                PropertyId.RollSpeed, PropertyId.NormalForce, PropertyId.LateralForce,
                PropertyId.DrivenVelocity, PropertyId.HeartOffset, PropertyId.Friction, PropertyId.Resistance
            };

            for (int i = 0; i < properties.Length; i++) {
                int index = PropertyIndex.ToIndex(properties[i], NodeType.Force);
                Assert.AreEqual(i, index, $"ToIndex({properties[i]}, Force) should be {i}");

                var prop = PropertyIndex.FromIndex(i, NodeType.Force);
                Assert.AreEqual(properties[i], prop, $"FromIndex({i}, Force) should be {properties[i]}");
            }
        }

        [Test]
        public void GeometricNode_PropertyIndexRoundTrip() {
            var properties = new[] {
                PropertyId.RollSpeed, PropertyId.PitchSpeed, PropertyId.YawSpeed,
                PropertyId.DrivenVelocity, PropertyId.HeartOffset, PropertyId.Friction, PropertyId.Resistance
            };

            for (int i = 0; i < properties.Length; i++) {
                int index = PropertyIndex.ToIndex(properties[i], NodeType.Geometric);
                Assert.AreEqual(i, index, $"ToIndex({properties[i]}, Geometric) should be {i}");

                var prop = PropertyIndex.FromIndex(i, NodeType.Geometric);
                Assert.AreEqual(properties[i], prop, $"FromIndex({i}, Geometric) should be {properties[i]}");
            }
        }

        [Test]
        public void ToIndex_NormalForce_Force_Returns1() {
            Assert.AreEqual(1, PropertyIndex.ToIndex(PropertyId.NormalForce, NodeType.Force));
        }

        [Test]
        public void ToIndex_PitchSpeed_Geometric_Returns1() {
            Assert.AreEqual(1, PropertyIndex.ToIndex(PropertyId.PitchSpeed, NodeType.Geometric));
        }

        [Test]
        public void Index1_Force_IsNormalForce() {
            Assert.AreEqual(PropertyId.NormalForce, PropertyIndex.FromIndex(1, NodeType.Force));
        }

        [Test]
        public void Index1_Geometric_IsPitchSpeed() {
            Assert.AreEqual(PropertyId.PitchSpeed, PropertyIndex.FromIndex(1, NodeType.Geometric));
        }

        [Test]
        public void Index2_Force_IsLateralForce() {
            Assert.AreEqual(PropertyId.LateralForce, PropertyIndex.FromIndex(2, NodeType.Force));
        }

        [Test]
        public void Index2_Geometric_IsYawSpeed() {
            Assert.AreEqual(PropertyId.YawSpeed, PropertyIndex.FromIndex(2, NodeType.Geometric));
        }

        [Test]
        public void ForceAndGeometric_ShareIndices1And2_DifferentSemantics() {
            Assert.AreEqual(1, PropertyIndex.ToIndex(PropertyId.NormalForce, NodeType.Force));
            Assert.AreEqual(1, PropertyIndex.ToIndex(PropertyId.PitchSpeed, NodeType.Geometric));
            Assert.AreNotEqual(PropertyIndex.FromIndex(1, NodeType.Force), PropertyIndex.FromIndex(1, NodeType.Geometric));

            Assert.AreEqual(2, PropertyIndex.ToIndex(PropertyId.LateralForce, NodeType.Force));
            Assert.AreEqual(2, PropertyIndex.ToIndex(PropertyId.YawSpeed, NodeType.Geometric));
            Assert.AreNotEqual(PropertyIndex.FromIndex(2, NodeType.Force), PropertyIndex.FromIndex(2, NodeType.Geometric));
        }

        [Test]
        public void InvalidPropertyForNode_ReturnsNegative() {
            Assert.AreEqual(-1, PropertyIndex.ToIndex(PropertyId.PitchSpeed, NodeType.Force));
            Assert.AreEqual(-1, PropertyIndex.ToIndex(PropertyId.NormalForce, NodeType.Geometric));
            Assert.AreEqual(-1, PropertyIndex.ToIndex(PropertyId.RollSpeed, NodeType.Anchor));
        }

        [Test]
        public void CurvedNode_PropertyIndexRoundTrip() {
            var properties = new[] {
                PropertyId.RollSpeed, PropertyId.DrivenVelocity, PropertyId.HeartOffset,
                PropertyId.Friction, PropertyId.Resistance
            };

            for (int i = 0; i < properties.Length; i++) {
                int index = PropertyIndex.ToIndex(properties[i], NodeType.Curved);
                Assert.AreEqual(i, index, $"ToIndex({properties[i]}, Curved) should be {i}");

                var prop = PropertyIndex.FromIndex(i, NodeType.Curved);
                Assert.AreEqual(properties[i], prop, $"FromIndex({i}, Curved) should be {properties[i]}");
            }
        }

        [Test]
        public void CopyPathNode_PropertyIndexRoundTrip() {
            var properties = new[] {
                PropertyId.DrivenVelocity, PropertyId.HeartOffset,
                PropertyId.Friction, PropertyId.Resistance
            };

            for (int i = 0; i < properties.Length; i++) {
                int index = PropertyIndex.ToIndex(properties[i], NodeType.CopyPath);
                Assert.AreEqual(i, index, $"ToIndex({properties[i]}, CopyPath) should be {i}");

                var prop = PropertyIndex.FromIndex(i, NodeType.CopyPath);
                Assert.AreEqual(properties[i], prop, $"FromIndex({i}, CopyPath) should be {properties[i]}");
            }
        }

        [Test]
        public void BridgeNode_PropertyIndexRoundTrip() {
            var properties = new[] {
                PropertyId.DrivenVelocity, PropertyId.HeartOffset, PropertyId.Friction,
                PropertyId.Resistance, PropertyId.TrackStyle
            };

            for (int i = 0; i < properties.Length; i++) {
                int index = PropertyIndex.ToIndex(properties[i], NodeType.Bridge);
                Assert.AreEqual(i, index, $"ToIndex({properties[i]}, Bridge) should be {i}");

                var prop = PropertyIndex.FromIndex(i, NodeType.Bridge);
                Assert.AreEqual(properties[i], prop, $"FromIndex({i}, Bridge) should be {properties[i]}");
            }
        }

        [TestCase(NodeType.Force, 7)]
        [TestCase(NodeType.Geometric, 7)]
        [TestCase(NodeType.Curved, 5)]
        [TestCase(NodeType.CopyPath, 4)]
        [TestCase(NodeType.Bridge, 5)]
        public void ToIndex_AllPropertiesForNodeType_AreUnique(NodeType type, int expectedCount) {
            var seen = new System.Collections.Generic.HashSet<int>();
            int count = NodeSchema.PropertyCount(type);
            Assert.AreEqual(expectedCount, count);

            for (int i = 0; i < count; i++) {
                PropertyId prop = NodeSchema.Property(type, i);
                int index = PropertyIndex.ToIndex(prop, type);
                Assert.IsTrue(seen.Add(index), $"{type} has duplicate index {index}");
            }
        }

        [TestCase(NodeType.Force, 7)]
        [TestCase(NodeType.Geometric, 7)]
        [TestCase(NodeType.Curved, 5)]
        [TestCase(NodeType.CopyPath, 4)]
        [TestCase(NodeType.Bridge, 5)]
        public void FromIndex_AllIndexesForNodeType_AreValid(NodeType type, int expectedCount) {
            for (int i = 0; i < expectedCount; i++) {
                PropertyId prop = PropertyIndex.FromIndex(i, type);
                Assert.AreNotEqual((PropertyId)255, prop, $"{type} FromIndex({i}) should return valid property");
            }
        }
    }
}
