using KexEdit.Spline.Rendering;
using NUnit.Framework;
using Unity.Collections;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class SegmentationMathTests {
        private const float TOLERANCE = 1e-4f;

        [Test]
        public void ComputeSegmentCount_100m_Returns10Segments() {
            int count = SegmentationMath.ComputeSegmentCount(100f, 10f);
            Assert.AreEqual(10, count);
        }

        [Test]
        public void ComputeSegmentCount_95m_Returns10Segments() {
            int count = SegmentationMath.ComputeSegmentCount(95f, 10f);
            Assert.AreEqual(10, count);
        }

        [Test]
        public void ComputeSegmentCount_105m_Returns10Segments() {
            int count = SegmentationMath.ComputeSegmentCount(105f, 10f);
            Assert.AreEqual(10, count);
        }

        [Test]
        public void ComputeSegmentCount_50m_Returns5Segments() {
            int count = SegmentationMath.ComputeSegmentCount(50f, 10f);
            Assert.AreEqual(5, count);
        }

        [Test]
        public void ComputeSegmentCount_5m_Returns1Segment() {
            int count = SegmentationMath.ComputeSegmentCount(5f, 10f);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void ComputeSegmentCount_0m_Returns0Segments() {
            int count = SegmentationMath.ComputeSegmentCount(0f, 10f);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void ComputeSegmentCount_VeryLong_ReturnsCorrectCount() {
            int count = SegmentationMath.ComputeSegmentCount(1000f, 10f);
            Assert.AreEqual(100, count);
        }

        [Test]
        public void ComputeSegments_100m_ProducesCorrectBoundaries() {
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            SegmentationMath.ComputeSegments(0f, 100f, 10f, ref output);

            Assert.AreEqual(10, output.Length);

            for (int i = 0; i < output.Length; i++) {
                Assert.AreEqual(i * 10f, output[i].StartArc, TOLERANCE);
                Assert.AreEqual((i + 1) * 10f, output[i].EndArc, TOLERANCE);
                Assert.AreEqual(1f, output[i].Scale, TOLERANCE);
            }

            output.Dispose();
        }

        [Test]
        public void ComputeSegments_95m_ProducesScaledBoundaries() {
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            SegmentationMath.ComputeSegments(0f, 95f, 10f, ref output);

            Assert.AreEqual(10, output.Length);

            float expectedScale = 9.5f / 10f;
            for (int i = 0; i < output.Length; i++) {
                Assert.AreEqual(expectedScale, output[i].Scale, TOLERANCE);
                Assert.AreEqual(9.5f, output[i].Length, TOLERANCE);
            }

            output.Dispose();
        }

        [Test]
        public void ComputeSegments_105m_ProducesScaledBoundaries() {
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            SegmentationMath.ComputeSegments(0f, 105f, 10f, ref output);

            Assert.AreEqual(10, output.Length);

            float expectedScale = 10.5f / 10f;
            for (int i = 0; i < output.Length; i++) {
                Assert.AreEqual(expectedScale, output[i].Scale, TOLERANCE);
                Assert.AreEqual(10.5f, output[i].Length, TOLERANCE);
            }

            output.Dispose();
        }

        [Test]
        public void ComputeSegments_NonZeroStart_OffsetsCorrectly() {
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            SegmentationMath.ComputeSegments(50f, 150f, 10f, ref output);

            Assert.AreEqual(10, output.Length);
            Assert.AreEqual(50f, output[0].StartArc, TOLERANCE);
            Assert.AreEqual(150f, output[^1].EndArc, TOLERANCE);

            output.Dispose();
        }

        [Test]
        public void ComputeSegments_0Length_ProducesEmpty() {
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            SegmentationMath.ComputeSegments(0f, 0f, 10f, ref output);

            Assert.AreEqual(0, output.Length);

            output.Dispose();
        }

        [Test]
        public void ComputeSegments_VeryShort_ProducesOneSegment() {
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            SegmentationMath.ComputeSegments(0f, 5f, 10f, ref output);

            Assert.AreEqual(1, output.Length);
            Assert.AreEqual(0f, output[0].StartArc, TOLERANCE);
            Assert.AreEqual(5f, output[0].EndArc, TOLERANCE);
            Assert.AreEqual(0.5f, output[0].Scale, TOLERANCE);

            output.Dispose();
        }

        [Test]
        public void ComputeSegments_ClearsOutputBeforeWrite() {
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);
            output.Add(new SegmentBoundary(999f, 1000f, 1f, 0));

            SegmentationMath.ComputeSegments(0f, 20f, 10f, ref output);

            Assert.AreEqual(2, output.Length);
            Assert.AreEqual(0f, output[0].StartArc, TOLERANCE);

            output.Dispose();
        }

        [Test]
        public void ComputeSegments_MultiPiece_100m_Uses10mPiece() {
            var pieces = new NativeArray<TrackPiece>(2, Allocator.Temp);
            pieces[0] = new TrackPiece(10f, 0);
            pieces[1] = new TrackPiece(2f, 1);
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            SegmentationMath.ComputeSegments(0f, 100f, pieces, SegmentationMath.DefaultTolerance, ref output);

            Assert.AreEqual(10, output.Length);
            Assert.AreEqual(0, output[0].PieceIndex);
            Assert.AreEqual(1f, output[0].Scale, TOLERANCE);

            pieces.Dispose();
            output.Dispose();
        }

        [Test]
        public void ComputeSegments_MultiPiece_3m_Uses2mPiece() {
            var pieces = new NativeArray<TrackPiece>(2, Allocator.Temp);
            pieces[0] = new TrackPiece(10f, 0);
            pieces[1] = new TrackPiece(2f, 1);
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            // 3m: 10m piece @ 0.3 scale = 70% skew (exceeds 50%), fallback to 2m @ 0.75
            SegmentationMath.ComputeSegments(0f, 3f, pieces, SegmentationMath.DefaultTolerance, ref output);

            Assert.AreEqual(2, output.Length);
            Assert.AreEqual(1, output[0].PieceIndex);
            Assert.AreEqual(0.75f, output[0].Scale, TOLERANCE);

            pieces.Dispose();
            output.Dispose();
        }

        [Test]
        public void ComputeSegments_MultiPiece_5m_Uses10mPieceAtBoundary() {
            var pieces = new NativeArray<TrackPiece>(2, Allocator.Temp);
            pieces[0] = new TrackPiece(10f, 0);
            pieces[1] = new TrackPiece(2f, 1);
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            // 5m with 10m piece = 1 segment at 5m, scale 0.5 (at 50% tolerance boundary)
            SegmentationMath.ComputeSegments(0f, 5f, pieces, SegmentationMath.DefaultTolerance, ref output);

            Assert.AreEqual(1, output.Length);
            Assert.AreEqual(0, output[0].PieceIndex);
            Assert.AreEqual(0.5f, output[0].Scale, TOLERANCE);

            pieces.Dispose();
            output.Dispose();
        }

        [Test]
        public void ComputeSegments_MultiPiece_11m_WithinTolerance_Uses10mPiece() {
            var pieces = new NativeArray<TrackPiece>(2, Allocator.Temp);
            pieces[0] = new TrackPiece(10f, 0);
            pieces[1] = new TrackPiece(2f, 1);
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            // 11m with 10m piece = 1 segment at 1.1 scale (within 50% tolerance)
            SegmentationMath.ComputeSegments(0f, 11f, pieces, SegmentationMath.DefaultTolerance, ref output);

            Assert.AreEqual(1, output.Length);
            Assert.AreEqual(0, output[0].PieceIndex);
            Assert.AreEqual(1.1f, output[0].Scale, TOLERANCE);

            pieces.Dispose();
            output.Dispose();
        }

        [Test]
        public void ComputeSegments_MultiPiece_2m_PerfectFit_Uses2mPiece() {
            var pieces = new NativeArray<TrackPiece>(2, Allocator.Temp);
            pieces[0] = new TrackPiece(10f, 0);
            pieces[1] = new TrackPiece(2f, 1);
            var output = new NativeList<SegmentBoundary>(16, Allocator.Temp);

            SegmentationMath.ComputeSegments(0f, 2f, pieces, SegmentationMath.DefaultTolerance, ref output);

            Assert.AreEqual(1, output.Length);
            Assert.AreEqual(1, output[0].PieceIndex);
            Assert.AreEqual(1f, output[0].Scale, TOLERANCE);

            pieces.Dispose();
            output.Dispose();
        }
    }
}
