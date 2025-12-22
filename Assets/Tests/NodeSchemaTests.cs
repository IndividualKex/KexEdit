using KexEdit.Sim.Schema;
using NUnit.Framework;
using System;
using Unity.Collections;

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
        [TestCase(NodeType.Bridge, 4)]
        [TestCase(NodeType.Anchor, 8)]
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
        [TestCase(NodeType.Force, 8)]
        [TestCase(NodeType.Geometric, 8)]
        [TestCase(NodeType.Curved, 6)]
        [TestCase(NodeType.CopyPath, 5)]
        [TestCase(NodeType.Bridge, 5)]
        [TestCase(NodeType.Anchor, 0)]
        [TestCase(NodeType.Reverse, 0)]
        [TestCase(NodeType.ReversePath, 0)]
        public void PropertyCount_AllNodeTypes_ReturnsExpectedCount(NodeType type, int expected) {
            int actual = NodeSchema.PropertyCount(type);
            Assert.AreEqual(expected, actual, $"{type} property count mismatch");
        }

        [Test]
        public void Property_InvalidIndex_Returns255() {
            PropertyId result = NodeSchema.Property(NodeType.Force, 99);
            Assert.AreEqual((PropertyId)255, result);
        }

        [Test]
        public void Property_Force_ReturnsEightProperties() {
            Assert.AreEqual(PropertyId.RollSpeed, NodeSchema.Property(NodeType.Force, 0));
            Assert.AreEqual(PropertyId.NormalForce, NodeSchema.Property(NodeType.Force, 1));
            Assert.AreEqual(PropertyId.LateralForce, NodeSchema.Property(NodeType.Force, 2));
            Assert.AreEqual(PropertyId.DrivenVelocity, NodeSchema.Property(NodeType.Force, 3));
            Assert.AreEqual(PropertyId.HeartOffset, NodeSchema.Property(NodeType.Force, 4));
            Assert.AreEqual(PropertyId.Friction, NodeSchema.Property(NodeType.Force, 5));
            Assert.AreEqual(PropertyId.Resistance, NodeSchema.Property(NodeType.Force, 6));
            Assert.AreEqual(PropertyId.TrackStyle, NodeSchema.Property(NodeType.Force, 7));
        }

        [Test]
        public void Property_Geometric_ReturnsEightProperties() {
            Assert.AreEqual(PropertyId.RollSpeed, NodeSchema.Property(NodeType.Geometric, 0));
            Assert.AreEqual(PropertyId.PitchSpeed, NodeSchema.Property(NodeType.Geometric, 1));
            Assert.AreEqual(PropertyId.YawSpeed, NodeSchema.Property(NodeType.Geometric, 2));
            Assert.AreEqual(PropertyId.DrivenVelocity, NodeSchema.Property(NodeType.Geometric, 3));
            Assert.AreEqual(PropertyId.HeartOffset, NodeSchema.Property(NodeType.Geometric, 4));
            Assert.AreEqual(PropertyId.Friction, NodeSchema.Property(NodeType.Geometric, 5));
            Assert.AreEqual(PropertyId.Resistance, NodeSchema.Property(NodeType.Geometric, 6));
            Assert.AreEqual(PropertyId.TrackStyle, NodeSchema.Property(NodeType.Geometric, 7));
        }

        [Test]
        public void Property_Force_And_Geometric_ShareRollSpeedAtIndex0() {
            Assert.AreEqual(NodeSchema.Property(NodeType.Force, 0), NodeSchema.Property(NodeType.Geometric, 0));
            Assert.AreEqual(PropertyId.RollSpeed, NodeSchema.Property(NodeType.Force, 0));
        }

        [Test]
        public void Property_Curved_ReturnsSixProperties() {
            Assert.AreEqual(PropertyId.RollSpeed, NodeSchema.Property(NodeType.Curved, 0));
            Assert.AreEqual(PropertyId.DrivenVelocity, NodeSchema.Property(NodeType.Curved, 1));
            Assert.AreEqual(PropertyId.HeartOffset, NodeSchema.Property(NodeType.Curved, 2));
            Assert.AreEqual(PropertyId.Friction, NodeSchema.Property(NodeType.Curved, 3));
            Assert.AreEqual(PropertyId.Resistance, NodeSchema.Property(NodeType.Curved, 4));
            Assert.AreEqual(PropertyId.TrackStyle, NodeSchema.Property(NodeType.Curved, 5));
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
    public class PortSpecSchemaTests {
        [Test]
        public void InputSpec_Force_ReturnsCorrectSpecs() {
            NodeSchema.InputSpec(NodeType.Force, 0, out var anchor);
            Assert.AreEqual(PortDataType.Anchor, anchor.DataType);
            Assert.AreEqual(0, anchor.LocalIndex);

            NodeSchema.InputSpec(NodeType.Force, 1, out var duration);
            Assert.AreEqual(PortDataType.Scalar, duration.DataType);
            Assert.AreEqual(0, duration.LocalIndex);
        }

        [Test]
        public void InputSpec_Curved_ScalarsHaveIncrementingIndices() {
            // Curved: Anchor(0), Radius(Scalar0), Arc(Scalar1), Axis(Scalar2), LeadIn(Scalar3), LeadOut(Scalar4)
            NodeSchema.InputSpec(NodeType.Curved, 0, out var anchor);
            Assert.AreEqual(PortDataType.Anchor, anchor.DataType);
            Assert.AreEqual(0, anchor.LocalIndex);

            for (int i = 1; i <= 5; i++) {
                NodeSchema.InputSpec(NodeType.Curved, i, out var spec);
                Assert.AreEqual(PortDataType.Scalar, spec.DataType, $"Input {i} should be Scalar");
                Assert.AreEqual(i - 1, spec.LocalIndex, $"Input {i} should have LocalIndex {i - 1}");
            }
        }

        [Test]
        public void InputSpec_Bridge_HasTwoAnchors() {
            // Bridge: Anchor(Anchor0), Target(Anchor1), OutWeight(Scalar1), InWeight(Scalar0)
            NodeSchema.InputSpec(NodeType.Bridge, 0, out var anchor);
            Assert.AreEqual(PortDataType.Anchor, anchor.DataType);
            Assert.AreEqual(0, anchor.LocalIndex);

            NodeSchema.InputSpec(NodeType.Bridge, 1, out var target);
            Assert.AreEqual(PortDataType.Anchor, target.DataType);
            Assert.AreEqual(1, target.LocalIndex);

            NodeSchema.InputSpec(NodeType.Bridge, 2, out var outWeight);
            Assert.AreEqual(PortDataType.Scalar, outWeight.DataType);
            Assert.AreEqual(1, outWeight.LocalIndex);

            NodeSchema.InputSpec(NodeType.Bridge, 3, out var inWeight);
            Assert.AreEqual(PortDataType.Scalar, inWeight.DataType);
            Assert.AreEqual(0, inWeight.LocalIndex);
        }

        [Test]
        public void InputSpec_Anchor_HasCorrectTypes() {
            // Anchor: Position(Vector0), Roll(Scalar0), Pitch(Scalar1), Yaw(Scalar2),
            //         Velocity(Scalar3), Heart(Scalar4), Friction(Scalar5), Resistance(Scalar6)
            NodeSchema.InputSpec(NodeType.Anchor, 0, out var position);
            Assert.AreEqual(PortDataType.Vector, position.DataType);
            Assert.AreEqual(0, position.LocalIndex);

            NodeSchema.InputSpec(NodeType.Anchor, 1, out var roll);
            Assert.AreEqual(PortDataType.Scalar, roll.DataType);
            Assert.AreEqual(0, roll.LocalIndex);

            NodeSchema.InputSpec(NodeType.Anchor, 2, out var pitch);
            Assert.AreEqual(PortDataType.Scalar, pitch.DataType);
            Assert.AreEqual(1, pitch.LocalIndex);

            NodeSchema.InputSpec(NodeType.Anchor, 3, out var yaw);
            Assert.AreEqual(PortDataType.Scalar, yaw.DataType);
            Assert.AreEqual(2, yaw.LocalIndex);

            NodeSchema.InputSpec(NodeType.Anchor, 4, out var velocity);
            Assert.AreEqual(PortDataType.Scalar, velocity.DataType);
            Assert.AreEqual(3, velocity.LocalIndex);

            NodeSchema.InputSpec(NodeType.Anchor, 5, out var heart);
            Assert.AreEqual(PortDataType.Scalar, heart.DataType);
            Assert.AreEqual(4, heart.LocalIndex);

            NodeSchema.InputSpec(NodeType.Anchor, 6, out var friction);
            Assert.AreEqual(PortDataType.Scalar, friction.DataType);
            Assert.AreEqual(5, friction.LocalIndex);

            NodeSchema.InputSpec(NodeType.Anchor, 7, out var resistance);
            Assert.AreEqual(PortDataType.Scalar, resistance.DataType);
            Assert.AreEqual(6, resistance.LocalIndex);
        }

        [Test]
        public void InputSpec_CopyPath_HasPathAndScalars() {
            // CopyPath: Anchor(Anchor0), Path(Path0), Start(Scalar0), End(Scalar1)
            NodeSchema.InputSpec(NodeType.CopyPath, 0, out var anchor);
            Assert.AreEqual(PortDataType.Anchor, anchor.DataType);

            NodeSchema.InputSpec(NodeType.CopyPath, 1, out var path);
            Assert.AreEqual(PortDataType.Path, path.DataType);
            Assert.AreEqual(0, path.LocalIndex);

            NodeSchema.InputSpec(NodeType.CopyPath, 2, out var start);
            Assert.AreEqual(PortDataType.Scalar, start.DataType);
            Assert.AreEqual(0, start.LocalIndex);

            NodeSchema.InputSpec(NodeType.CopyPath, 3, out var end);
            Assert.AreEqual(PortDataType.Scalar, end.DataType);
            Assert.AreEqual(1, end.LocalIndex);
        }

        [Test]
        public void OutputSpec_Force_ReturnsAnchorAndPath() {
            NodeSchema.OutputSpec(NodeType.Force, 0, out var anchor);
            Assert.AreEqual(PortDataType.Anchor, anchor.DataType);
            Assert.AreEqual(0, anchor.LocalIndex);

            NodeSchema.OutputSpec(NodeType.Force, 1, out var path);
            Assert.AreEqual(PortDataType.Path, path.DataType);
            Assert.AreEqual(0, path.LocalIndex);
        }

        [Test]
        public void OutputSpec_Scalar_ReturnsScalar() {
            NodeSchema.OutputSpec(NodeType.Scalar, 0, out var scalar);
            Assert.AreEqual(PortDataType.Scalar, scalar.DataType);
            Assert.AreEqual(0, scalar.LocalIndex);
        }

        [Test]
        public void OutputSpec_Vector_ReturnsVector() {
            NodeSchema.OutputSpec(NodeType.Vector, 0, out var vector);
            Assert.AreEqual(PortDataType.Vector, vector.DataType);
            Assert.AreEqual(0, vector.LocalIndex);
        }

        [Test]
        public void InputSpec_InvalidIndex_ReturnsInvalid() {
            NodeSchema.InputSpec(NodeType.Force, 99, out var invalid);
            Assert.IsFalse(invalid.IsValid);
        }

        [Test]
        public void OutputSpec_InvalidIndex_ReturnsInvalid() {
            NodeSchema.OutputSpec(NodeType.Force, 99, out var invalid);
            Assert.IsFalse(invalid.IsValid);
        }

        [Test]
        public void InputName_Force_ReturnsCorrectNames() {
            NodeSchema.InputName(NodeType.Force, 0, out var name0);
            NodeSchema.InputName(NodeType.Force, 1, out var name1);
            Assert.AreEqual("Anchor", name0.ToString());
            Assert.AreEqual("Duration", name1.ToString());
        }

        [Test]
        public void InputName_Curved_ReturnsCorrectNames() {
            NodeSchema.InputName(NodeType.Curved, 0, out var name0);
            NodeSchema.InputName(NodeType.Curved, 1, out var name1);
            NodeSchema.InputName(NodeType.Curved, 2, out var name2);
            NodeSchema.InputName(NodeType.Curved, 3, out var name3);
            NodeSchema.InputName(NodeType.Curved, 4, out var name4);
            NodeSchema.InputName(NodeType.Curved, 5, out var name5);
            Assert.AreEqual("Anchor", name0.ToString());
            Assert.AreEqual("Radius", name1.ToString());
            Assert.AreEqual("Arc", name2.ToString());
            Assert.AreEqual("Axis", name3.ToString());
            Assert.AreEqual("Lead In", name4.ToString());
            Assert.AreEqual("Lead Out", name5.ToString());
        }

        [Test]
        public void InputName_Bridge_ReturnsCorrectNames() {
            NodeSchema.InputName(NodeType.Bridge, 0, out var name0);
            NodeSchema.InputName(NodeType.Bridge, 1, out var name1);
            NodeSchema.InputName(NodeType.Bridge, 2, out var name2);
            NodeSchema.InputName(NodeType.Bridge, 3, out var name3);
            Assert.AreEqual("Anchor", name0.ToString());
            Assert.AreEqual("Target", name1.ToString());
            Assert.AreEqual("Out Weight", name2.ToString());
            Assert.AreEqual("In Weight", name3.ToString());
        }

        [Test]
        public void InputName_Anchor_ReturnsCorrectNames() {
            NodeSchema.InputName(NodeType.Anchor, 0, out var name0);
            NodeSchema.InputName(NodeType.Anchor, 1, out var name1);
            NodeSchema.InputName(NodeType.Anchor, 2, out var name2);
            NodeSchema.InputName(NodeType.Anchor, 3, out var name3);
            NodeSchema.InputName(NodeType.Anchor, 4, out var name4);
            NodeSchema.InputName(NodeType.Anchor, 5, out var name5);
            NodeSchema.InputName(NodeType.Anchor, 6, out var name6);
            NodeSchema.InputName(NodeType.Anchor, 7, out var name7);
            Assert.AreEqual("Position", name0.ToString());
            Assert.AreEqual("Roll", name1.ToString());
            Assert.AreEqual("Pitch", name2.ToString());
            Assert.AreEqual("Yaw", name3.ToString());
            Assert.AreEqual("Velocity", name4.ToString());
            Assert.AreEqual("Heart", name5.ToString());
            Assert.AreEqual("Friction", name6.ToString());
            Assert.AreEqual("Resistance", name7.ToString());
        }

        [Test]
        public void OutputName_Force_ReturnsCorrectNames() {
            NodeSchema.OutputName(NodeType.Force, 0, out var name0);
            NodeSchema.OutputName(NodeType.Force, 1, out var name1);
            Assert.AreEqual("Anchor", name0.ToString());
            Assert.AreEqual("Path", name1.ToString());
        }

        [Test]
        public void DefaultInputValue_Duration_Returns5() {
            Assert.AreEqual(5f, NodeSchema.DefaultInputValue(NodeType.Force, 1));
        }

        [Test]
        public void DefaultInputValue_CurvedRadius_Returns20() {
            Assert.AreEqual(20f, NodeSchema.DefaultInputValue(NodeType.Curved, 1));
        }

        [Test]
        public void DefaultInputValue_CurvedArc_Returns90() {
            Assert.AreEqual(90f, NodeSchema.DefaultInputValue(NodeType.Curved, 2));
        }

        [Test]
        public void DefaultInputValue_BridgeWeights_Return0Point5() {
            Assert.AreEqual(0.5f, NodeSchema.DefaultInputValue(NodeType.Bridge, 2));
            Assert.AreEqual(0.5f, NodeSchema.DefaultInputValue(NodeType.Bridge, 3));
        }

        [Test]
        public void DefaultInputValue_CopyPathStartEnd_Return0And1() {
            Assert.AreEqual(0f, NodeSchema.DefaultInputValue(NodeType.CopyPath, 2));
            Assert.AreEqual(1f, NodeSchema.DefaultInputValue(NodeType.CopyPath, 3));
        }

        [Test]
        public void AllNodeTypes_InputSpecsMatchInputCount() {
            var allTypes = (NodeType[])Enum.GetValues(typeof(NodeType));
            foreach (NodeType type in allTypes) {
                int count = NodeSchema.InputCount(type);
                for (int i = 0; i < count; i++) {
                    NodeSchema.InputSpec(type, i, out var spec);
                    Assert.IsTrue(spec.IsValid, $"{type} input {i} should have valid spec");
                }
                if (count > 0) {
                    NodeSchema.InputSpec(type, count, out var invalid);
                    Assert.IsFalse(invalid.IsValid, $"{type} should return invalid spec at index {count}");
                }
            }
        }

        [Test]
        public void AllNodeTypes_OutputSpecsMatchOutputCount() {
            var allTypes = (NodeType[])Enum.GetValues(typeof(NodeType));
            foreach (NodeType type in allTypes) {
                int count = NodeSchema.OutputCount(type);
                for (int i = 0; i < count; i++) {
                    NodeSchema.OutputSpec(type, i, out var spec);
                    Assert.IsTrue(spec.IsValid, $"{type} output {i} should have valid spec");
                }
                if (count > 0) {
                    NodeSchema.OutputSpec(type, count, out var invalid);
                    Assert.IsFalse(invalid.IsValid, $"{type} should return invalid spec at index {count}");
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
                PropertyId.DrivenVelocity, PropertyId.HeartOffset, PropertyId.Friction, PropertyId.Resistance, PropertyId.TrackStyle
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
                PropertyId.DrivenVelocity, PropertyId.HeartOffset, PropertyId.Friction, PropertyId.Resistance, PropertyId.TrackStyle
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
                PropertyId.Friction, PropertyId.Resistance, PropertyId.TrackStyle
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
                PropertyId.Friction, PropertyId.Resistance, PropertyId.TrackStyle
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

        [TestCase(NodeType.Force, 8)]
        [TestCase(NodeType.Geometric, 8)]
        [TestCase(NodeType.Curved, 6)]
        [TestCase(NodeType.CopyPath, 5)]
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

        [TestCase(NodeType.Force, 8)]
        [TestCase(NodeType.Geometric, 8)]
        [TestCase(NodeType.Curved, 6)]
        [TestCase(NodeType.CopyPath, 5)]
        [TestCase(NodeType.Bridge, 5)]
        public void FromIndex_AllIndexesForNodeType_AreValid(NodeType type, int expectedCount) {
            for (int i = 0; i < expectedCount; i++) {
                PropertyId prop = PropertyIndex.FromIndex(i, type);
                Assert.AreNotEqual((PropertyId)255, prop, $"{type} FromIndex({i}) should return valid property");
            }
        }
    }
}
