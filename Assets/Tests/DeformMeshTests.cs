using KexEdit.Spline;
using KexEdit.Spline.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    [Category("Unit")]
    public class DeformMeshTests {
        private const float TOLERANCE = 1e-3f;
        private const float NOMINAL_LENGTH = 10f;

        private struct SimpleMesh {
            public NativeArray<float3> Vertices;
            public NativeArray<float3> Normals;
            public NativeArray<int> Indices;

            public void Dispose() {
                if (Vertices.IsCreated) Vertices.Dispose();
                if (Normals.IsCreated) Normals.Dispose();
                if (Indices.IsCreated) Indices.Dispose();
            }
        }

        private SimpleMesh CreateTestSegmentMesh() {
            var mesh = new SimpleMesh {
                Vertices = new NativeArray<float3>(4, Allocator.Temp),
                Normals = new NativeArray<float3>(4, Allocator.Temp),
                Indices = new NativeArray<int>(6, Allocator.Temp)
            };

            mesh.Vertices[0] = new float3(-0.5f, 0f, 0f);
            mesh.Vertices[1] = new float3(0.5f, 0f, 0f);
            mesh.Vertices[2] = new float3(-0.5f, 0f, 10f);
            mesh.Vertices[3] = new float3(0.5f, 0f, 10f);

            for (int i = 0; i < 4; i++) {
                mesh.Normals[i] = math.up();
            }

            mesh.Indices[0] = 0; mesh.Indices[1] = 2; mesh.Indices[2] = 1;
            mesh.Indices[3] = 1; mesh.Indices[4] = 2; mesh.Indices[5] = 3;

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
        public void Mesh_StraightPath_PreservesVertexCount() {
            var mesh = CreateTestSegmentMesh();
            var spline = CreateStraightSpline(20f);
            float startArc = 0f;
            float segmentLength = 10f;

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                startArc,
                segmentLength,
                NOMINAL_LENGTH,
                ref outputPositions,
                ref outputNormals
            );

            Assert.AreEqual(mesh.Vertices.Length, outputPositions.Length);
            Assert.AreEqual(mesh.Normals.Length, outputNormals.Length);

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }

        [Test]
        public void Mesh_StraightPath_PreservesLength() {
            var mesh = CreateTestSegmentMesh();
            var spline = CreateStraightSpline(20f);
            float startArc = 0f;
            float segmentLength = 10f;

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                startArc,
                segmentLength,
                NOMINAL_LENGTH,
                ref outputPositions,
                ref outputNormals
            );

            float startZ = (outputPositions[0].z + outputPositions[1].z) / 2f;
            float endZ = (outputPositions[2].z + outputPositions[3].z) / 2f;

            Assert.AreEqual(0f, startZ, TOLERANCE);
            Assert.AreEqual(-10f, endZ, TOLERANCE);

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }

        [Test]
        public void Mesh_ScaledSegment_AppliesStretchFactor() {
            var mesh = CreateTestSegmentMesh();
            var spline = CreateStraightSpline(20f);
            float startArc = 0f;
            float segmentLength = 9.5f;

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                startArc,
                segmentLength,
                NOMINAL_LENGTH,
                ref outputPositions,
                ref outputNormals
            );

            float startZ = (outputPositions[0].z + outputPositions[1].z) / 2f;
            float endZ = (outputPositions[2].z + outputPositions[3].z) / 2f;

            Assert.AreEqual(0f, startZ, TOLERANCE);
            Assert.AreEqual(-9.5f, endZ, TOLERANCE);

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }

        [Test]
        public void Mesh_CurvedPath_DeformsCorrectly() {
            var mesh = CreateTestSegmentMesh();
            float radius = 10f / (math.PI * 0.5f);
            var spline = CreateQuarterCircleSpline(radius);
            float startArc = 0f;
            float segmentLength = radius * math.PI * 0.5f;

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                startArc,
                segmentLength,
                NOMINAL_LENGTH,
                ref outputPositions,
                ref outputNormals
            );

            float startX = (outputPositions[0].x + outputPositions[1].x) / 2f;
            float startZ = (outputPositions[0].z + outputPositions[1].z) / 2f;
            Assert.AreEqual(0f, startX, 0.1f);
            Assert.AreEqual(0f, startZ, 0.1f);

            float endX = (outputPositions[2].x + outputPositions[3].x) / 2f;
            float endZ = (outputPositions[2].z + outputPositions[3].z) / 2f;
            Assert.AreEqual(radius, endX, 0.2f);
            Assert.AreEqual(-radius, endZ, 0.2f);

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }

        [Test]
        public void Mesh_WithOffset_StartsAtCorrectArc() {
            var mesh = CreateTestSegmentMesh();
            var spline = CreateStraightSpline(30f);
            float startArc = 10f;
            float segmentLength = 10f;

            var outputPositions = new NativeArray<float3>(mesh.Vertices.Length, Allocator.Temp);
            var outputNormals = new NativeArray<float3>(mesh.Normals.Length, Allocator.Temp);

            Deform.Mesh(
                mesh.Vertices,
                mesh.Normals,
                spline,
                startArc,
                segmentLength,
                NOMINAL_LENGTH,
                ref outputPositions,
                ref outputNormals
            );

            float startZ = (outputPositions[0].z + outputPositions[1].z) / 2f;
            float endZ = (outputPositions[2].z + outputPositions[3].z) / 2f;

            Assert.AreEqual(-10f, startZ, TOLERANCE);
            Assert.AreEqual(-20f, endZ, TOLERANCE);

            outputPositions.Dispose();
            outputNormals.Dispose();
            mesh.Dispose();
            spline.Dispose();
        }
    }
}
