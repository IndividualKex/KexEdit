using KexEdit.Spline;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class ArticulationTests {
        private const float TOLERANCE = 1e-5f;

        private NativeArray<SplinePoint> CreateStraightSpline(int count, float spacing) {
            var spline = new NativeArray<SplinePoint>(count, Allocator.Temp);
            for (int i = 0; i < count; i++) {
                float arc = i * spacing;
                float3 position = new float3(0f, 0f, -arc);
                spline[i] = new SplinePoint(arc, position, math.back(), math.down(), math.right());
            }
            return spline;
        }

        [Test]
        public void SplinePoint_Lerp_InterpolatesCorrectly() {
            var a = new SplinePoint(0f, float3.zero, math.back(), math.down(), math.right());
            var b = new SplinePoint(10f, new float3(0, 0, -10), math.back(), math.down(), math.right());

            SplinePoint.Lerp(a, b, 0.5f, out SplinePoint mid);

            Assert.AreEqual(5f, mid.Arc, TOLERANCE);
            Assert.AreEqual(-5f, mid.Position.z, TOLERANCE);
        }

        [Test]
        public void SplinePoint_LocalToWorld_AppliesOffset() {
            var point = new SplinePoint(0f, new float3(10, 5, -20), math.back(), math.down(), math.right());
            float3 local = new float3(2f, 1f, 3f);

            float3 world = point.LocalToWorld(local);

            float3 expected = point.Position
                + point.Direction * local.x
                + point.Normal * local.y
                + point.Lateral * local.z;

            Assert.AreEqual(expected.x, world.x, TOLERANCE);
            Assert.AreEqual(expected.y, world.y, TOLERANCE);
            Assert.AreEqual(expected.z, world.z, TOLERANCE);
        }

        [Test]
        public void SplineInterpolation_FindIndex_AtStart_ReturnsZero() {
            var spline = CreateStraightSpline(10, 10f);
            int index = SplineInterpolation.FindIndex(spline, 0f);
            Assert.AreEqual(0, index);
            spline.Dispose();
        }

        [Test]
        public void SplineInterpolation_FindIndex_AtEnd_ReturnsLastSegment() {
            var spline = CreateStraightSpline(10, 10f);
            int index = SplineInterpolation.FindIndex(spline, 90f);
            Assert.AreEqual(8, index);
            spline.Dispose();
        }

        [Test]
        public void SplineInterpolation_FindIndex_InMiddle_ReturnsCorrectSegment() {
            var spline = CreateStraightSpline(10, 10f);
            int index = SplineInterpolation.FindIndex(spline, 45f);
            Assert.AreEqual(4, index);
            spline.Dispose();
        }

        [Test]
        public void SplineInterpolation_Interpolate_AtMidpoint_InterpolatesCorrectly() {
            var spline = CreateStraightSpline(10, 10f);
            SplineInterpolation.Interpolate(spline, 25f, out SplinePoint point);
            Assert.AreEqual(25f, point.Arc, TOLERANCE);
            Assert.AreEqual(-25f, point.Position.z, TOLERANCE);
            spline.Dispose();
        }
    }
}
