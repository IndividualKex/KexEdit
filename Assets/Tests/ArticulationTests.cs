using KexEdit.Core.Articulation;
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

        private NativeArray<SplinePoint> CreateCurvedSpline() {
            var spline = new NativeArray<SplinePoint>(5, Allocator.Temp);
            spline[0] = new SplinePoint(0f, new float3(0, 0, 0), math.back(), math.down(), math.right());
            spline[1] = new SplinePoint(10f, new float3(5, 0, -8), math.normalize(new float3(-0.6f, 0, -0.8f)), math.down(), math.normalize(new float3(-0.8f, 0, 0.6f)));
            spline[2] = new SplinePoint(20f, new float3(10, 0, -12), math.left(), math.down(), math.back());
            spline[3] = new SplinePoint(30f, new float3(18, 0, -7), math.normalize(new float3(-0.6f, 0, 0.8f)), math.down(), math.normalize(new float3(0.8f, 0, 0.6f)));
            spline[4] = new SplinePoint(40f, new float3(20, 0, 0), math.forward(), math.down(), math.left());
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

        [Test]
        public void AnchorOffset_ConstructsCorrectly() {
            var offset = new AnchorOffset(5f, new float3(0, 1, 0));
            Assert.AreEqual(5f, offset.Arc, TOLERANCE);
            Assert.AreEqual(1f, offset.Local.y, TOLERANCE);
        }

        [Test]
        public void Anchor_RotationPointsAlongDirection() {
            var anchor = new Anchor(float3.zero, math.back(), math.down(), math.right(), 0f);
            float3 forward = math.mul(anchor.Rotation, math.forward());
            Assert.AreEqual(math.back().x, forward.x, TOLERANCE);
            Assert.AreEqual(math.back().y, forward.y, TOLERANCE);
            Assert.AreEqual(math.back().z, forward.z, TOLERANCE);
        }

        [Test]
        public void AnchorPositioning_OnSpline_ReturnsCorrectPosition() {
            var spline = CreateStraightSpline(10, 10f);
            AnchorPositioning.Position(spline, 25f, out Anchor anchor);

            Assert.AreEqual(25f, anchor.Arc, TOLERANCE);
            Assert.AreEqual(-25f, anchor.Position.z, TOLERANCE);
            spline.Dispose();
        }

        [Test]
        public void AnchorPositioning_BeforeSpline_ProjectsAlongTangent() {
            var spline = CreateStraightSpline(10, 10f);
            AnchorPositioning.Position(spline, -5f, out Anchor anchor);

            Assert.AreEqual(-5f, anchor.Arc, TOLERANCE);
            Assert.AreEqual(5f, anchor.Position.z, TOLERANCE);
            spline.Dispose();
        }

        [Test]
        public void AnchorPositioning_AfterSpline_ProjectsAlongTangent() {
            var spline = CreateStraightSpline(10, 10f);
            AnchorPositioning.Position(spline, 100f, out Anchor anchor);

            Assert.AreEqual(100f, anchor.Arc, TOLERANCE);
            Assert.AreEqual(-100f, anchor.Position.z, TOLERANCE);
            spline.Dispose();
        }

        [Test]
        public void AnchorPositioning_WithLocalOffset_AppliesOffset() {
            var spline = CreateStraightSpline(10, 10f);
            float3 localOffset = new float3(0f, 2f, 0f);
            AnchorPositioning.Position(spline, 50f, localOffset, out Anchor anchor);

            Assert.AreEqual(50f, anchor.Arc, TOLERANCE);
            float3 expectedPos = new float3(0, 0, -50) + math.down() * 2f;
            Assert.AreEqual(expectedPos.x, anchor.Position.x, TOLERANCE);
            Assert.AreEqual(expectedPos.y, anchor.Position.y, TOLERANCE);
            Assert.AreEqual(expectedPos.z, anchor.Position.z, TOLERANCE);
            spline.Dispose();
        }

        [Test]
        public void AnchorPositioning_WithAnchorOffset_AppliesArcAndLocal() {
            var spline = CreateStraightSpline(10, 10f);
            var offset = new AnchorOffset(10f, new float3(0f, 1f, 0f));
            AnchorPositioning.Position(spline, 40f, offset, out Anchor anchor);

            Assert.AreEqual(50f, anchor.Arc, TOLERANCE);
            spline.Dispose();
        }

        [Test]
        public void BodyPositioning_FromSingleAnchor_UsesAnchorTransform() {
            var anchor = new Anchor(new float3(1, 2, 3), math.back(), math.down(), math.right(), 10f);
            BodyPositioning.FromAnchor(anchor, out BodyTransform body);

            Assert.AreEqual(anchor.Position.x, body.Position.x, TOLERANCE);
            Assert.AreEqual(anchor.Position.y, body.Position.y, TOLERANCE);
            Assert.AreEqual(anchor.Position.z, body.Position.z, TOLERANCE);
        }

        [Test]
        public void BodyPositioning_FromTwoAnchors_ComputesForwardFromPositions() {
            var leading = new Anchor(new float3(0, 0, 0), math.back(), math.down(), math.right(), 0f);
            var trailing = new Anchor(new float3(0, 0, -10), math.back(), math.down(), math.right(), 10f);

            BodyPositioning.FromAnchors(leading, trailing, 0f, out BodyTransform body);

            float3 forward = math.mul(body.Rotation, math.forward());
            Assert.AreEqual(0f, forward.x, TOLERANCE);
            Assert.AreEqual(0f, forward.y, TOLERANCE);
            Assert.Less(forward.z, 0f);
        }

        [Test]
        public void BodyPositioning_FromTwoAnchors_PivotZeroAtLeading() {
            var leading = new Anchor(new float3(0, 0, 0), math.back(), math.down(), math.right(), 0f);
            var trailing = new Anchor(new float3(0, 0, -10), math.back(), math.down(), math.right(), 10f);

            BodyPositioning.FromAnchors(leading, trailing, 0f, out BodyTransform body);

            Assert.AreEqual(leading.Position.x, body.Position.x, TOLERANCE);
            Assert.AreEqual(leading.Position.y, body.Position.y, TOLERANCE);
            Assert.AreEqual(leading.Position.z, body.Position.z, TOLERANCE);
        }

        [Test]
        public void BodyPositioning_FromTwoAnchors_PivotOneAtTrailing() {
            var leading = new Anchor(new float3(0, 0, 0), math.back(), math.down(), math.right(), 0f);
            var trailing = new Anchor(new float3(0, 0, -10), math.back(), math.down(), math.right(), 10f);

            BodyPositioning.FromAnchors(leading, trailing, 1f, out BodyTransform body);

            Assert.AreEqual(trailing.Position.x, body.Position.x, TOLERANCE);
            Assert.AreEqual(trailing.Position.y, body.Position.y, TOLERANCE);
            Assert.AreEqual(trailing.Position.z, body.Position.z, TOLERANCE);
        }

        [Test]
        public void BodyPositioning_FromTwoAnchors_PivotHalfAtCenter() {
            var leading = new Anchor(new float3(0, 0, 0), math.back(), math.down(), math.right(), 0f);
            var trailing = new Anchor(new float3(0, 0, -10), math.back(), math.down(), math.right(), 10f);

            BodyPositioning.FromAnchors(leading, trailing, 0.5f, out BodyTransform body);

            float3 expectedPos = (leading.Position + trailing.Position) * 0.5f;
            Assert.AreEqual(expectedPos.x, body.Position.x, TOLERANCE);
            Assert.AreEqual(expectedPos.y, body.Position.y, TOLERANCE);
            Assert.AreEqual(expectedPos.z, body.Position.z, TOLERANCE);
        }

        [Test]
        public void BodyPositioning_FromTwoAnchors_AveragesUpVectors() {
            var leading = new Anchor(new float3(0, 0, 0), math.back(), math.down(), math.right(), 0f);
            var trailing = new Anchor(new float3(0, 0, -10), math.normalize(new float3(0, 0.1f, -1)), math.normalize(new float3(0, -1, -0.1f)), math.right(), 10f);

            BodyPositioning.FromAnchors(leading, trailing, 0f, out BodyTransform body);

            float3 up = math.mul(body.Rotation, math.up());
            Assert.Greater(up.y, 0.9f);
        }

        [Test]
        public void BodyPositioning_FromMultipleAnchors_UsesFirstAndLast() {
            var anchors = new NativeArray<Anchor>(3, Allocator.Temp);
            anchors[0] = new Anchor(new float3(0, 0, 0), math.back(), math.down(), math.right(), 0f);
            anchors[1] = new Anchor(new float3(5, 0, -5), math.back(), math.down(), math.right(), 7f);
            anchors[2] = new Anchor(new float3(10, 0, -10), math.back(), math.down(), math.right(), 14f);

            BodyPositioning.FromAnchors(anchors, 0f, out BodyTransform body);

            float3 forward = math.mul(body.Rotation, math.forward());
            float3 expectedForward = math.normalize(anchors[2].Position - anchors[0].Position);
            Assert.AreEqual(expectedForward.x, forward.x, TOLERANCE);
            Assert.AreEqual(expectedForward.y, forward.y, TOLERANCE);
            Assert.AreEqual(expectedForward.z, forward.z, TOLERANCE);

            anchors.Dispose();
        }

        [Test]
        public void BodyPositioning_FromMultipleAnchors_ByPivotIndex() {
            var anchors = new NativeArray<Anchor>(3, Allocator.Temp);
            anchors[0] = new Anchor(new float3(0, 0, 0), math.back(), math.down(), math.right(), 0f);
            anchors[1] = new Anchor(new float3(5, 0, -5), math.back(), math.down(), math.right(), 7f);
            anchors[2] = new Anchor(new float3(10, 0, -10), math.back(), math.down(), math.right(), 14f);

            BodyPositioning.FromAnchors(anchors, 1, out BodyTransform body);

            Assert.AreEqual(anchors[1].Position.x, body.Position.x, TOLERANCE);
            Assert.AreEqual(anchors[1].Position.y, body.Position.y, TOLERANCE);
            Assert.AreEqual(anchors[1].Position.z, body.Position.z, TOLERANCE);

            anchors.Dispose();
        }

        [Test]
        public void CurvedSpline_AnchorFollowsCurve() {
            var spline = CreateCurvedSpline();
            AnchorPositioning.Position(spline, 20f, out Anchor mid);

            Assert.AreEqual(10f, mid.Position.x, 0.1f);
            Assert.AreEqual(-12f, mid.Position.z, 0.1f);
            spline.Dispose();
        }

        [Test]
        public void CurvedSpline_BodyInterpolatesRotation() {
            var spline = CreateCurvedSpline();

            AnchorPositioning.Position(spline, 18f, out Anchor leading);
            AnchorPositioning.Position(spline, 22f, out Anchor trailing);

            BodyPositioning.FromAnchors(leading, trailing, 0.5f, out BodyTransform body);

            float3 forward = math.mul(body.Rotation, math.forward());
            Assert.AreNotEqual(0f, forward.x, "Body should be rotated on curve");
            spline.Dispose();
        }

        [Test]
        public void SwappingAnchorOrder_FlipsBodyDirection() {
            var spline = CreateStraightSpline(10, 10f);

            AnchorPositioning.Position(spline, 40f, out Anchor a);
            AnchorPositioning.Position(spline, 60f, out Anchor b);

            BodyPositioning.FromAnchors(a, b, 0f, out BodyTransform fwd);
            BodyPositioning.FromAnchors(b, a, 0f, out BodyTransform reversed);

            float3 forwardDir = math.mul(fwd.Rotation, math.forward());
            float3 reversedDir = math.mul(reversed.Rotation, math.forward());

            float dot = math.dot(forwardDir, reversedDir);
            Assert.Less(dot, -0.9f, "Swapping anchor order should flip direction");

            spline.Dispose();
        }
    }
}
