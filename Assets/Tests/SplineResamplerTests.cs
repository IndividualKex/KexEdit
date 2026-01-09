using KexEdit.Spline;
using KexEdit.Spline.Resampling;
using KexEdit.Sim;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class SplineResamplerTests {
        private const float TOLERANCE = 1e-4f;

        private NativeArray<Point> CreateStraightPath(int count, float spacing, float heartOffset = 1.1f) {
            var points = new NativeArray<Point>(count, Allocator.Temp);
            for (int i = 0; i < count; i++) {
                float arc = i * spacing;
                float3 heartPos = new float3(0f, heartOffset, -arc);
                points[i] = new Point(
                    heartPosition: heartPos,
                    direction: math.back(),
                    normal: math.down(),
                    lateral: math.right(),
                    velocity: 10f,
                    normalForce: 1f,
                    lateralForce: 0f,
                    heartArc: arc,
                    spineArc: arc,
                    heartAdvance: spacing,
                    frictionOrigin: 0f,
                    heartOffset: heartOffset
                );
            }
            return points;
        }

        [Test]
        public void Resample_Direct_PreservesPointCount() {
            var points = CreateStraightPath(10, 1f);
            var output = new NativeList<SplinePoint>(Allocator.Temp);

            SplineResampler.Resample(in points, ref output);

            Assert.AreEqual(10, output.Length);

            output.Dispose();
            points.Dispose();
        }

        [Test]
        public void Resample_Direct_ConvertsToSpinePosition() {
            float heartOffset = 1.1f;
            var points = CreateStraightPath(5, 1f, heartOffset);
            var output = new NativeList<SplinePoint>(Allocator.Temp);

            SplineResampler.Resample(in points, ref output);

            for (int i = 0; i < output.Length; i++) {
                float3 expectedPos = points[i].SpinePosition(heartOffset);
                Assert.AreEqual(expectedPos.x, output[i].Position.x, TOLERANCE);
                Assert.AreEqual(expectedPos.y, output[i].Position.y, TOLERANCE);
                Assert.AreEqual(expectedPos.z, output[i].Position.z, TOLERANCE);
            }

            output.Dispose();
            points.Dispose();
        }

        [Test]
        public void Resample_Direct_PreservesArc() {
            var points = CreateStraightPath(10, 5f);
            var output = new NativeList<SplinePoint>(Allocator.Temp);

            SplineResampler.Resample(in points, ref output);

            for (int i = 0; i < output.Length; i++) {
                Assert.AreEqual(points[i].SpineArc, output[i].Arc, TOLERANCE);
            }

            output.Dispose();
            points.Dispose();
        }

        [Test]
        public void Resample_ArcLength_ProducesUniformSpacing() {
            var points = CreateStraightPath(100, 0.1f);
            float resolution = 1f;
            var output = new NativeList<SplinePoint>(Allocator.Temp);

            SplineResampler.Resample(in points, resolution, ref output);

            float totalLength = points[^1].SpineArc - points[0].SpineArc;
            int expectedSamples = (int)math.ceil(totalLength / resolution) + 1;
            Assert.That(output.Length, Is.InRange(expectedSamples - 1, expectedSamples + 1));

            for (int i = 1; i < output.Length; i++) {
                float arcDelta = output[i].Arc - output[i - 1].Arc;
                Assert.That(arcDelta, Is.EqualTo(resolution).Within(0.02f));
            }

            output.Dispose();
            points.Dispose();
        }

        [Test]
        public void Resample_ArcLength_InterpolatesPosition() {
            var points = CreateStraightPath(10, 10f);
            float resolution = 5f;
            var output = new NativeList<SplinePoint>(Allocator.Temp);

            SplineResampler.Resample(in points, resolution, ref output);

            foreach (var sp in output) {
                Assert.AreEqual(-sp.Arc, sp.Position.z, TOLERANCE, $"Position.z should match -Arc for straight path at arc={sp.Arc}");
            }

            output.Dispose();
            points.Dispose();
        }

        [Test]
        public void Resample_EmptyInput_ProducesEmptyOutput() {
            var points = new NativeArray<Point>(0, Allocator.Temp);
            var output = new NativeList<SplinePoint>(Allocator.Temp);

            SplineResampler.Resample(in points, ref output);
            Assert.AreEqual(0, output.Length);

            SplineResampler.Resample(in points, 1f, ref output);
            Assert.AreEqual(0, output.Length);

            output.Dispose();
            points.Dispose();
        }

        [Test]
        public void Resample_SinglePoint_ProducesSingleOutput() {
            var points = CreateStraightPath(1, 0f);
            var output = new NativeList<SplinePoint>(Allocator.Temp);

            SplineResampler.Resample(in points, ref output);
            Assert.AreEqual(1, output.Length);

            output.Clear();
            SplineResampler.Resample(in points, 1f, ref output);
            Assert.AreEqual(1, output.Length);

            output.Dispose();
            points.Dispose();
        }

        [Test]
        public void ToSplinePoint_ConvertsCorrectly() {
            float heartOffset = 1.5f;
            var point = new Point(
                heartPosition: new float3(1, 2, 3),
                direction: math.back(),
                normal: math.down(),
                lateral: math.right(),
                velocity: 15f,
                normalForce: 1.5f,
                lateralForce: 0.3f,
                heartArc: 50f,
                spineArc: 55f,
                heartAdvance: 1f,
                frictionOrigin: 0f,
                heartOffset: heartOffset
            );

            SplineResampler.ToSplinePoint(in point, out var splinePoint);

            Assert.AreEqual(point.SpineArc, splinePoint.Arc, TOLERANCE);

            float3 expectedPos = point.SpinePosition(heartOffset);
            Assert.AreEqual(expectedPos.x, splinePoint.Position.x, TOLERANCE);
            Assert.AreEqual(expectedPos.y, splinePoint.Position.y, TOLERANCE);
            Assert.AreEqual(expectedPos.z, splinePoint.Position.z, TOLERANCE);

            Assert.AreEqual(point.Direction.z, splinePoint.Direction.z, TOLERANCE);
            Assert.AreEqual(point.Normal.y, splinePoint.Normal.y, TOLERANCE);
            Assert.AreEqual(point.Lateral.x, splinePoint.Lateral.x, TOLERANCE);
        }
    }
}
