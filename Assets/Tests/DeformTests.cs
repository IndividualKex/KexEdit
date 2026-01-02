using KexEdit.Spline;
using KexEdit.Spline.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class DeformTests {
        private const float TOLERANCE = 1e-4f;
        private const float NOMINAL_LENGTH = 10f;

        private NativeArray<SplinePoint> CreateStraightSpline(float length, float resolution = 0.1f) {
            int count = (int)math.ceil(length / resolution) + 1;
            var points = new NativeArray<SplinePoint>(count, Allocator.Temp);

            for (int i = 0; i < count; i++) {
                float arc = i * resolution;
                points[i] = new SplinePoint(
                    arc,
                    new float3(0f, 0f, -arc),
                    math.back(),
                    math.down(),
                    math.right()
                );
            }

            return points;
        }

        private NativeArray<SplinePoint> CreateQuarterCircleSpline(float radius, float resolution = 0.1f) {
            float arcLength = radius * math.PI * 0.5f;
            int count = (int)math.ceil(arcLength / resolution) + 1;
            var points = new NativeArray<SplinePoint>(count, Allocator.Temp);

            for (int i = 0; i < count; i++) {
                float arc = i * resolution;
                float angle = arc / radius;

                float3 position = new float3(
                    radius * (1f - math.cos(angle)),
                    0f,
                    -radius * math.sin(angle)
                );
                float3 direction = math.normalize(new float3(math.sin(angle), 0f, -math.cos(angle)));
                float3 normal = math.down();
                float3 lateral = math.normalize(math.cross(direction, normal));

                points[i] = new SplinePoint(arc, position, direction, normal, lateral);
            }

            return points;
        }

        [Test]
        public void Arc_AtSegmentStart_ReturnsStartArc() {
            float startArc = 50f;
            float length = 10f;
            float localZ = 0f;

            float result = Deform.Arc(localZ, startArc, length, NOMINAL_LENGTH);

            Assert.AreEqual(startArc, result, TOLERANCE);
        }

        [Test]
        public void Arc_AtSegmentEnd_ReturnsEndArc() {
            float startArc = 50f;
            float length = 10f;
            float localZ = NOMINAL_LENGTH;

            float result = Deform.Arc(localZ, startArc, length, NOMINAL_LENGTH);

            Assert.AreEqual(startArc + length, result, TOLERANCE);
        }

        [Test]
        public void Arc_AtMidpoint_ReturnsMiddleArc() {
            float startArc = 0f;
            float length = 10f;
            float localZ = 5f;

            float result = Deform.Arc(localZ, startArc, length, NOMINAL_LENGTH);

            Assert.AreEqual(5f, result, TOLERANCE);
        }

        [Test]
        public void Arc_WithScale_AdjustsCorrectly() {
            float startArc = 0f;
            float length = 9.5f;
            float localZ = NOMINAL_LENGTH;

            float result = Deform.Arc(localZ, startArc, length, NOMINAL_LENGTH);

            Assert.AreEqual(9.5f, result, TOLERANCE);
        }

        [Test]
        public void Position_StraightPath_PreservesPosition() {
            var spline = CreateStraightSpline(20f);
            float3 localVertex = new float3(0.5f, -0.2f, 5f);
            float startArc = 0f;
            float length = 10f;

            Deform.Position(
                localVertex,
                spline,
                startArc,
                length,
                NOMINAL_LENGTH,
                out float3 result
            );

            Assert.AreEqual(0.5f, result.x, TOLERANCE);
            Assert.AreEqual(-0.2f, result.y, TOLERANCE);
            Assert.AreEqual(-5f, result.z, TOLERANCE);

            spline.Dispose();
        }

        [Test]
        public void Position_StraightPath_PreservesOffset() {
            var spline = CreateStraightSpline(30f);
            float3 localVertex = new float3(1f, 0f, 5f);
            float startArc = 10f;
            float length = 10f;

            Deform.Position(
                localVertex,
                spline,
                startArc,
                length,
                NOMINAL_LENGTH,
                out float3 result
            );

            Assert.AreEqual(1f, result.x, TOLERANCE);
            Assert.AreEqual(0f, result.y, TOLERANCE);
            Assert.AreEqual(-15f, result.z, TOLERANCE);

            spline.Dispose();
        }

        [Test]
        public void Position_QuarterCircle_DeformsCorrectly() {
            float radius = 10f;
            var spline = CreateQuarterCircleSpline(radius);
            float arcLength = radius * math.PI * 0.5f;

            float3 localVertex = new float3(0f, 0f, NOMINAL_LENGTH);
            float startArc = 0f;

            Deform.Position(
                localVertex,
                spline,
                startArc,
                arcLength,
                NOMINAL_LENGTH,
                out float3 result
            );

            Assert.AreEqual(radius, result.x, 0.05f);
            Assert.AreEqual(0f, result.y, TOLERANCE);
            Assert.AreEqual(-radius, result.z, 0.05f);

            spline.Dispose();
        }

        [Test]
        public void Position_WithScale_StretchesZAxis() {
            var spline = CreateStraightSpline(20f);
            float3 localVertex = new float3(0f, 0f, 10f);
            float startArc = 0f;
            float length = 9.5f;

            Deform.Position(
                localVertex,
                spline,
                startArc,
                length,
                NOMINAL_LENGTH,
                out float3 result
            );

            Assert.AreEqual(-9.5f, result.z, TOLERANCE);

            spline.Dispose();
        }

        [Test]
        public void Normal_StraightPath_PreservesNormal() {
            var spline = CreateStraightSpline(20f);
            float3 localNormal = math.down();
            float arc = 5f;

            Deform.Normal(localNormal, spline, arc, out float3 result);

            Assert.AreEqual(localNormal.x, result.x, TOLERANCE);
            Assert.AreEqual(localNormal.y, result.y, TOLERANCE);
            Assert.AreEqual(localNormal.z, result.z, TOLERANCE);

            spline.Dispose();
        }

        [Test]
        public void Normal_QuarterCircle_RotatesNormal() {
            float radius = 10f;
            var spline = CreateQuarterCircleSpline(radius);
            float3 localNormal = math.down();
            float arc = radius * math.PI * 0.5f;

            Deform.Normal(localNormal, spline, arc, out float3 result);

            Assert.AreEqual(0f, result.x, 0.05f);
            Assert.AreEqual(1f, result.y, 0.05f);
            Assert.AreEqual(0f, result.z, 0.05f);

            spline.Dispose();
        }

        [Test]
        public void Vertex_StraightPath_TransformsBoth() {
            var spline = CreateStraightSpline(20f);
            float3 localPos = new float3(0.5f, -0.2f, 5f);
            float3 localNormal = math.down();
            float startArc = 0f;
            float length = 10f;

            Deform.Vertex(
                localPos,
                localNormal,
                spline,
                startArc,
                length,
                NOMINAL_LENGTH,
                out float3 worldPos,
                out float3 worldNormal
            );

            Assert.AreEqual(0.5f, worldPos.x, TOLERANCE);
            Assert.AreEqual(-0.2f, worldPos.y, TOLERANCE);
            Assert.AreEqual(-5f, worldPos.z, TOLERANCE);
            Assert.AreEqual(0f, worldNormal.x, TOLERANCE);
            Assert.AreEqual(-1f, worldNormal.y, TOLERANCE);
            Assert.AreEqual(0f, worldNormal.z, TOLERANCE);

            spline.Dispose();
        }
    }
}
