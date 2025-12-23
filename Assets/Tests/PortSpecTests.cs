using KexEdit.Sim.Schema;
using NUnit.Framework;

namespace Tests {
    [TestFixture]
    [Category("Schema")]
    public class PortSpecTests {
        [Test]
        public void Constructor_StoresDataTypeAndIndex() {
            var spec = new PortSpec(PortDataType.Scalar, 3);

            Assert.AreEqual(PortDataType.Scalar, spec.DataType);
            Assert.AreEqual(3, spec.LocalIndex);
        }

        [Test]
        public void Constructor_DefaultIndex_IsZero() {
            var spec = new PortSpec(PortDataType.Vector);

            Assert.AreEqual(PortDataType.Vector, spec.DataType);
            Assert.AreEqual(0, spec.LocalIndex);
        }

        [Test]
        public void ToEncoded_FromEncoded_Roundtrip() {
            var original = new PortSpec(PortDataType.Anchor, 5);

            uint encoded = original.ToEncoded();
            PortSpec.FromEncoded(encoded, out var decoded);

            Assert.AreEqual(original, decoded);
        }

        [Test]
        public void ToEncoded_AllDataTypes_Roundtrip() {
            var dataTypes = new[] {
                PortDataType.Scalar,
                PortDataType.Vector,
                PortDataType.Anchor,
                PortDataType.Path
            };

            foreach (var dt in dataTypes) {
                for (byte i = 0; i < 10; i++) {
                    var original = new PortSpec(dt, i);
                    PortSpec.FromEncoded(original.ToEncoded(), out var decoded);
                    Assert.AreEqual(original, decoded, $"Failed for {dt}, index {i}");
                }
            }
        }

        [Test]
        public void Equals_SameValues_ReturnsTrue() {
            var a = new PortSpec(PortDataType.Path, 2);
            var b = new PortSpec(PortDataType.Path, 2);

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [Test]
        public void Equals_DifferentDataType_ReturnsFalse() {
            var a = new PortSpec(PortDataType.Scalar, 0);
            var b = new PortSpec(PortDataType.Vector, 0);

            Assert.IsFalse(a.Equals(b));
            Assert.IsFalse(a == b);
            Assert.IsTrue(a != b);
        }

        [Test]
        public void Equals_DifferentIndex_ReturnsFalse() {
            var a = new PortSpec(PortDataType.Scalar, 0);
            var b = new PortSpec(PortDataType.Scalar, 1);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void GetHashCode_EqualSpecs_SameHash() {
            var a = new PortSpec(PortDataType.Anchor, 7);
            var b = new PortSpec(PortDataType.Anchor, 7);

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Default_IsScalarZero() {
            var spec = default(PortSpec);

            Assert.AreEqual(PortDataType.Scalar, spec.DataType);
            Assert.AreEqual(0, spec.LocalIndex);
        }

        [Test]
        public void IsValid_ValidSpec_ReturnsTrue() {
            var spec = new PortSpec(PortDataType.Scalar, 0);
            Assert.IsTrue(spec.IsValid);
        }

        [Test]
        public void IsValid_InvalidSpec_ReturnsFalse() {
            var spec = PortSpec.Invalid;
            Assert.IsFalse(spec.IsValid);
        }

        [Test]
        public void Invalid_HasMaxValues() {
            var invalid = PortSpec.Invalid;
            Assert.AreEqual(255, (byte)invalid.DataType);
            Assert.AreEqual(255, invalid.LocalIndex);
        }
    }
}
