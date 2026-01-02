using KexEdit.Spline;
using KexEdit.Spline.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class TrackMeshExporterTests {
        private const float TOLERANCE = 1e-3f;
        private const float NOMINAL_LENGTH = 10f;

        private struct SimpleMesh {
            public NativeArray<float3> Vertices;
            public NativeArray<float3> Normals;

            public void Dispose() {
                if (Vertices.IsCreated) Vertices.Dispose();
                if (Normals.IsCreated) Normals.Dispose();
            }
        }

        private SimpleMesh CreateTestSegmentMesh() {
            var mesh = new SimpleMesh {
                Vertices = new NativeArray<float3>(4, Allocator.Temp),
                Normals = new NativeArray<float3>(4, Allocator.Temp)
            };

            mesh.Vertices[0] = new float3(-0.5f, 0f, 0f);
            mesh.Vertices[1] = new float3(0.5f, 0f, 0f);
            mesh.Vertices[2] = new float3(-0.5f, 0f, NOMINAL_LENGTH);
            mesh.Vertices[3] = new float3(0.5f, 0f, NOMINAL_LENGTH);

            for (int i = 0; i < 4; i++) {
                mesh.Normals[i] = math.up();
            }

            return mesh;
        }

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

        [Test]
        public void SegmentDeformation_UsesSegmentStartArc_NotSectionArc() {
            // Arrange: Create a 100m section with segment at arc 50-60
            var mesh = CreateTestSegmentMesh();
            var spline = CreateStraightSpline(110f);

            // Create a segment boundary like what SegmentBuilder produces:
            // This segment is the 6th segment (index 5) in a 100m section with 10m segments
            var segment = new GPUSegmentBoundary {
                StartArc = 50f,     // Segment starts at arc 50 (correct value to use)
                Length = 10f,       // Segment length (correct value to use)
                ArcStart = 0f,      // Section starts at arc 0 (WRONG to use for deformation)
                ArcLength = 100f,   // Section total arc (WRONG to use for deformation)
                SplineStartIndex = 0,
                SplineCount = spline.Length,
                PieceIndex = 0,
                SectionIndex = 0
            };

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            // Act: Deform using CORRECT parameters (StartArc, Length)
            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                segment.StartArc,   // Correct: segment's start arc
                segment.Length,     // Correct: segment's length
                NOMINAL_LENGTH,
                ref outputPositions,
                ref outputNormals
            );

            // Assert: Mesh should start at Z=-50 and end at Z=-60
            float startZ = (outputPositions[0].z + outputPositions[1].z) / 2f;
            float endZ = (outputPositions[2].z + outputPositions[3].z) / 2f;

            Assert.AreEqual(-50f, startZ, TOLERANCE, "Segment start should be at arc 50 (Z=-50)");
            Assert.AreEqual(-60f, endZ, TOLERANCE, "Segment end should be at arc 60 (Z=-60)");

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }

        [Test]
        public void SegmentDeformation_WrongParameters_WouldStartAtSectionArc() {
            // This test documents the BUG behavior when using ArcStart/ArcLength instead of StartArc/Length
            var mesh = CreateTestSegmentMesh();
            var spline = CreateStraightSpline(110f);

            var segment = new GPUSegmentBoundary {
                StartArc = 50f,     // Segment starts at arc 50
                Length = 10f,       // Segment length
                ArcStart = 0f,      // Section starts at arc 0
                ArcLength = 100f,   // Section total arc
                SplineStartIndex = 0,
                SplineCount = spline.Length,
                PieceIndex = 0,
                SectionIndex = 0
            };

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            // Act: Deform using WRONG parameters (ArcStart, ArcLength) - this is the bug
            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                segment.ArcStart,   // Wrong: section's start arc (0)
                segment.ArcLength,  // Wrong: section's total length (100)
                NOMINAL_LENGTH,
                ref outputPositions,
                ref outputNormals
            );

            // Assert: With wrong params, mesh would span 0 to -100 instead of -50 to -60
            float startZ = (outputPositions[0].z + outputPositions[1].z) / 2f;
            float endZ = (outputPositions[2].z + outputPositions[3].z) / 2f;

            // This shows the buggy behavior: starts at 0 instead of -50
            Assert.AreEqual(0f, startZ, TOLERANCE, "With ArcStart, segment incorrectly starts at section start");
            Assert.AreEqual(-100f, endZ, TOLERANCE, "With ArcLength, segment incorrectly spans entire section");

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }

        [Test]
        public void SegmentDeformation_MultipleSegments_EachDeformsToCorrectRange() {
            // Verify that multiple segments in a section each deform to their own arc range
            var mesh = CreateTestSegmentMesh();
            var spline = CreateStraightSpline(110f);

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            // Test segments 0, 5, and 9 from a 10-segment section
            int[] segmentIndices = { 0, 5, 9 };

            foreach (int segIdx in segmentIndices) {
                var segment = new GPUSegmentBoundary {
                    StartArc = segIdx * 10f,
                    Length = 10f,
                    ArcStart = 0f,
                    ArcLength = 100f,
                    SplineStartIndex = 0,
                    SplineCount = spline.Length,
                    PieceIndex = 0,
                    SectionIndex = 0
                };

                Deform.Mesh(
                    mesh.Vertices,
                    mesh.Normals,
                    spline,
                    segment.StartArc,
                    segment.Length,
                    NOMINAL_LENGTH,
                    ref outputPositions,
                    ref outputNormals
                );

                float expectedStartZ = -segIdx * 10f;
                float expectedEndZ = -(segIdx + 1) * 10f;

                float startZ = (outputPositions[0].z + outputPositions[1].z) / 2f;
                float endZ = (outputPositions[2].z + outputPositions[3].z) / 2f;

                Assert.AreEqual(expectedStartZ, startZ, TOLERANCE, $"Segment {segIdx} start");
                Assert.AreEqual(expectedEndZ, endZ, TOLERANCE, $"Segment {segIdx} end");
            }

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }

        [Test]
        public void DeformedMesh_VertexNormals_ConsistentWithFaceWinding() {
            // Regression test: After deformation, vertex normals should be consistent
            // with the geometric face normal computed from vertex positions.
            // This catches bugs where coordinate flips or winding changes cause mismatches.

            // Create a simple quad mesh with known normal
            var mesh = CreateTestSegmentMesh();  // Has normals pointing up (0, 1, 0)
            var spline = CreateStraightSpline(20f);

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                0f,             // startArc
                NOMINAL_LENGTH, // segmentLength
                NOMINAL_LENGTH, // nominalLength
                ref outputPositions,
                ref outputNormals
            );

            // Compute geometric face normal from first triangle (vertices 0, 1, 2)
            // Unity winding is clockwise for front-facing
            float3 v0 = outputPositions[0];
            float3 v1 = outputPositions[1];
            float3 v2 = outputPositions[2];

            float3 edge1 = v1 - v0;
            float3 edge2 = v2 - v0;
            float3 geometricNormal = math.normalize(math.cross(edge1, edge2));

            // The vertex normal should point in the same general direction as the geometric normal
            // (dot product > 0, meaning less than 90 degrees apart)
            float3 vertexNormal = outputNormals[0];
            float dot = math.dot(geometricNormal, vertexNormal);

            Assert.Greater(dot, 0f, $"Vertex normal should face same direction as geometric normal. " +
                $"Geometric: ({geometricNormal.x:F3}, {geometricNormal.y:F3}, {geometricNormal.z:F3}), " +
                $"Vertex: ({vertexNormal.x:F3}, {vertexNormal.y:F3}, {vertexNormal.z:F3}), Dot: {dot:F3}");

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }

        [Test]
        public void ObjExport_HandednessConversion_GeometricNormalMatchesVertexNormal() {
            // Regression test: OBJ export must convert Unity (left-handed) to OBJ (right-handed).
            // This requires both Z negation AND winding flip. Either alone produces inverted normals.
            //
            // This test simulates the export transform and verifies the geometric normal
            // (computed from CCW winding, standard for OBJ) matches the exported vertex normal.

            var mesh = CreateTestSegmentMesh();
            var spline = CreateStraightSpline(20f);

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                0f,
                NOMINAL_LENGTH,
                NOMINAL_LENGTH,
                ref outputPositions,
                ref outputNormals
            );

            // Apply OBJ export transforms: negate Z on positions and normals
            var exportedPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var exportedNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            for (int i = 0; i < outputPositions.Length; i++) {
                var p = outputPositions[i];
                exportedPositions[i] = new float3(p.x, p.y, -p.z);  // Negate Z
                var n = outputNormals[i];
                exportedNormals[i] = new float3(n.x, n.y, -n.z);    // Negate Z
            }

            // Simulate winding flip: triangle 0-1-2 becomes 2-1-0
            float3 v0 = exportedPositions[2];
            float3 v1 = exportedPositions[1];
            float3 v2 = exportedPositions[0];

            // Compute geometric normal using CCW convention (standard for OBJ)
            float3 edge1 = v1 - v0;
            float3 edge2 = v2 - v0;
            float3 geometricNormal = math.normalize(math.cross(edge1, edge2));

            // The exported vertex normal should match the geometric normal direction
            float3 vertexNormal = exportedNormals[0];
            float dot = math.dot(geometricNormal, vertexNormal);

            Assert.Greater(dot, 0f,
                $"After handedness conversion, geometric normal should match vertex normal. " +
                $"Geometric: ({geometricNormal.x:F3}, {geometricNormal.y:F3}, {geometricNormal.z:F3}), " +
                $"Vertex: ({vertexNormal.x:F3}, {vertexNormal.y:F3}, {vertexNormal.z:F3}), Dot: {dot:F3}");

            exportedPositions.Dispose();
            exportedNormals.Dispose();
            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }
    }
}
