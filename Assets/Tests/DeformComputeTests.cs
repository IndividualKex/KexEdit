using System.Collections;
using KexEdit.Spline;
using KexEdit.Spline.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Tests {
    [TestFixture]
    [Category("GPU")]
    public class DeformComputeTests {
        private const float TOLERANCE = 1e-2f;
        private const float NOMINAL_LENGTH = 10f;

        private ComputeShader _computeShader;

        [OneTimeSetUp]
        public void LoadShader() {
            _computeShader = Resources.Load<ComputeShader>("DeformCompute");
        }

        private NativeArray<float3> CreateQuadVertices(Allocator allocator) {
            var vertices = new NativeArray<float3>(4, allocator);
            vertices[0] = new float3(-0.5f, 0f, 0f);
            vertices[1] = new float3(0.5f, 0f, 0f);
            vertices[2] = new float3(-0.5f, 0f, 10f);
            vertices[3] = new float3(0.5f, 0f, 10f);
            return vertices;
        }

        private NativeArray<float3> CreateQuadNormals(Allocator allocator) {
            var normals = new NativeArray<float3>(4, allocator);
            for (int i = 0; i < 4; i++) {
                normals[i] = math.up();
            }
            return normals;
        }

        private NativeArray<float4> CreateDefaultMask(int count, Allocator allocator) {
            var mask = new NativeArray<float4>(count, allocator);
            for (int i = 0; i < count; i++) {
                mask[i] = new float4(1, 1, 1, 1);
            }
            return mask;
        }

        private NativeArray<float> CreateDefaultData(int count, Allocator allocator) {
            var data = new NativeArray<float>(count, allocator);
            for (int i = 0; i < count; i++) {
                data[i] = 0f;
            }
            return data;
        }

        private NativeArray<float> CreateSectionHighlights(int count, Allocator allocator) {
            var highlights = new NativeArray<float>(count, allocator);
            for (int i = 0; i < count; i++) {
                highlights[i] = 0f;
            }
            return highlights;
        }

        private NativeArray<GPUSplinePoint> CreateStraightGPUSpline(float length, Allocator allocator, float resolution = 0.1f) {
            int count = (int)math.ceil(length / resolution) + 1;
            var points = new NativeArray<GPUSplinePoint>(count, allocator);

            for (int i = 0; i < count; i++) {
                float arc = i * resolution;
                points[i] = new GPUSplinePoint {
                    Arc = arc,
                    Position = new float3(0f, 0f, -arc),
                    Direction = math.back(),
                    Normal = math.down(),
                    Lateral = math.right()
                };
            }

            return points;
        }

        private NativeArray<SplinePoint> CreateStraightSpline(float length, Allocator allocator, float resolution = 0.1f) {
            int count = (int)math.ceil(length / resolution) + 1;
            var points = new NativeArray<SplinePoint>(count, allocator);

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

        [UnityTest]
        public IEnumerator Kernel_StraightPath_MatchesCPUReference() {
            if (_computeShader == null) {
                Assert.Ignore("Compute shader not found - skipping GPU test");
                yield break;
            }

            var vertices = CreateQuadVertices(Allocator.Persistent);
            var normals = CreateQuadNormals(Allocator.Persistent);
            var mask = CreateDefaultMask(4, Allocator.Persistent);
            var gpuSpline = CreateStraightGPUSpline(20f, Allocator.Persistent);
            var cpuSpline = CreateStraightSpline(20f, Allocator.Persistent);
            var data = CreateDefaultData(gpuSpline.Length, Allocator.Persistent);

            int vertexCount = vertices.Length;
            int segmentCount = 1;

            var segments = new NativeArray<GPUSegmentBoundary>(1, Allocator.Persistent);
            segments[0] = new GPUSegmentBoundary {
                StartArc = 0f,
                Length = 10f,
                Scale = 1f,
                ArcStart = 0f,
                ArcLength = 20f,
                SplineStartIndex = 0,
                SplineCount = gpuSpline.Length,
                PieceIndex = 0,
                SectionIndex = 0
            };
            var sectionHighlights = CreateSectionHighlights(1, Allocator.Persistent);

            var vertexBuffer = new ComputeBuffer(vertexCount, 3 * sizeof(float));
            var normalBuffer = new ComputeBuffer(vertexCount, 3 * sizeof(float));
            var maskBuffer = new ComputeBuffer(vertexCount, 4 * sizeof(float));
            var splineBuffer = new ComputeBuffer(gpuSpline.Length, GPUSplinePoint.Stride);
            var dataBuffer = new ComputeBuffer(gpuSpline.Length, sizeof(float));
            var highlightBuffer = new ComputeBuffer(1, sizeof(float));
            var segmentBuffer = new ComputeBuffer(segmentCount, GPUSegmentBoundary.Stride);
            var outputVertexBuffer = new ComputeBuffer(vertexCount * segmentCount, 3 * sizeof(float));
            var outputNormalBuffer = new ComputeBuffer(vertexCount * segmentCount, 3 * sizeof(float));
            var outputMaskBuffer = new ComputeBuffer(vertexCount * segmentCount, 4 * sizeof(float));
            var outputDataBuffer = new ComputeBuffer(vertexCount * segmentCount, 4 * sizeof(float));

            try {
                vertexBuffer.SetData(vertices);
                normalBuffer.SetData(normals);
                maskBuffer.SetData(mask);
                splineBuffer.SetData(gpuSpline);
                dataBuffer.SetData(data);
                highlightBuffer.SetData(sectionHighlights);
                segmentBuffer.SetData(segments);

                int kernel = _computeShader.FindKernel("DeformKernel");
                _computeShader.SetBuffer(kernel, "_SourceVertices", vertexBuffer);
                _computeShader.SetBuffer(kernel, "_SourceNormals", normalBuffer);
                _computeShader.SetBuffer(kernel, "_SourceMask", maskBuffer);
                _computeShader.SetBuffer(kernel, "_SplinePoints", splineBuffer);
                _computeShader.SetBuffer(kernel, "_SourceData", dataBuffer);
                _computeShader.SetBuffer(kernel, "_SectionHighlights", highlightBuffer);
                _computeShader.SetBuffer(kernel, "_Segments", segmentBuffer);
                _computeShader.SetBuffer(kernel, "_OutputVertices", outputVertexBuffer);
                _computeShader.SetBuffer(kernel, "_OutputNormals", outputNormalBuffer);
                _computeShader.SetBuffer(kernel, "_OutputMask", outputMaskBuffer);
                _computeShader.SetBuffer(kernel, "_OutputData", outputDataBuffer);
                _computeShader.SetInt("_VertexCount", vertexCount);
                _computeShader.SetInt("_SegmentCount", segmentCount);
                _computeShader.SetFloat("_NominalLength", NOMINAL_LENGTH);

                int threadGroups = (int)math.ceil((vertexCount * segmentCount) / 64f);
                _computeShader.Dispatch(kernel, threadGroups, 1, 1);

                var readbackVertices = AsyncGPUReadback.Request(outputVertexBuffer);
                var readbackNormals = AsyncGPUReadback.Request(outputNormalBuffer);

                yield return new WaitUntil(() => readbackVertices.done && readbackNormals.done);

                if (readbackVertices.hasError || readbackNormals.hasError) {
                    Assert.Fail("GPU readback failed");
                    yield break;
                }

                var gpuVertices = readbackVertices.GetData<float3>();
                var gpuNormals = readbackNormals.GetData<float3>();

                var cpuVertices = new NativeArray<float3>(vertexCount, Allocator.Persistent);
                var cpuNormals = new NativeArray<float3>(vertexCount, Allocator.Persistent);
                Deform.Mesh(vertices, normals, cpuSpline, 0f, 10f, NOMINAL_LENGTH,
                    ref cpuVertices, ref cpuNormals);

                for (int i = 0; i < vertexCount; i++) {
                    Assert.AreEqual(cpuVertices[i].x, gpuVertices[i].x, TOLERANCE,
                        $"Vertex {i} X mismatch: CPU={cpuVertices[i].x}, GPU={gpuVertices[i].x}");
                    Assert.AreEqual(cpuVertices[i].y, gpuVertices[i].y, TOLERANCE,
                        $"Vertex {i} Y mismatch: CPU={cpuVertices[i].y}, GPU={gpuVertices[i].y}");
                    Assert.AreEqual(cpuVertices[i].z, gpuVertices[i].z, TOLERANCE,
                        $"Vertex {i} Z mismatch: CPU={cpuVertices[i].z}, GPU={gpuVertices[i].z}");
                }

                cpuVertices.Dispose();
                cpuNormals.Dispose();
            }
            finally {
                vertexBuffer.Dispose();
                normalBuffer.Dispose();
                maskBuffer.Dispose();
                splineBuffer.Dispose();
                dataBuffer.Dispose();
                highlightBuffer.Dispose();
                segmentBuffer.Dispose();
                outputVertexBuffer.Dispose();
                outputNormalBuffer.Dispose();
                outputMaskBuffer.Dispose();
                outputDataBuffer.Dispose();
                vertices.Dispose();
                normals.Dispose();
                mask.Dispose();
                gpuSpline.Dispose();
                data.Dispose();
                sectionHighlights.Dispose();
                cpuSpline.Dispose();
                segments.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Kernel_MultipleSegments_ProcessesAll() {
            if (_computeShader == null) {
                Assert.Ignore("Compute shader not found - skipping GPU test");
                yield break;
            }

            var vertices = CreateQuadVertices(Allocator.Persistent);
            var normals = CreateQuadNormals(Allocator.Persistent);
            var mask = CreateDefaultMask(4, Allocator.Persistent);
            var gpuSpline = CreateStraightGPUSpline(40f, Allocator.Persistent);
            var data = CreateDefaultData(gpuSpline.Length, Allocator.Persistent);

            int vertexCount = vertices.Length;
            int segmentCount = 3;

            var segments = new NativeArray<GPUSegmentBoundary>(segmentCount, Allocator.Persistent);
            segments[0] = new GPUSegmentBoundary { StartArc = 0f, Length = 10f, Scale = 1f, ArcStart = 0f, ArcLength = 40f, SplineStartIndex = 0, SplineCount = gpuSpline.Length, PieceIndex = 0, SectionIndex = 0 };
            segments[1] = new GPUSegmentBoundary { StartArc = 10f, Length = 10f, Scale = 1f, ArcStart = 0f, ArcLength = 40f, SplineStartIndex = 0, SplineCount = gpuSpline.Length, PieceIndex = 0, SectionIndex = 0 };
            segments[2] = new GPUSegmentBoundary { StartArc = 20f, Length = 10f, Scale = 1f, ArcStart = 0f, ArcLength = 40f, SplineStartIndex = 0, SplineCount = gpuSpline.Length, PieceIndex = 0, SectionIndex = 0 };
            var sectionHighlights = CreateSectionHighlights(1, Allocator.Persistent);

            var vertexBuffer = new ComputeBuffer(vertexCount, 3 * sizeof(float));
            var normalBuffer = new ComputeBuffer(vertexCount, 3 * sizeof(float));
            var maskBuffer = new ComputeBuffer(vertexCount, 4 * sizeof(float));
            var splineBuffer = new ComputeBuffer(gpuSpline.Length, GPUSplinePoint.Stride);
            var dataBuffer = new ComputeBuffer(gpuSpline.Length, sizeof(float));
            var highlightBuffer = new ComputeBuffer(1, sizeof(float));
            var segmentBuffer = new ComputeBuffer(segmentCount, GPUSegmentBoundary.Stride);
            var outputVertexBuffer = new ComputeBuffer(vertexCount * segmentCount, 3 * sizeof(float));
            var outputNormalBuffer = new ComputeBuffer(vertexCount * segmentCount, 3 * sizeof(float));
            var outputMaskBuffer = new ComputeBuffer(vertexCount * segmentCount, 4 * sizeof(float));
            var outputDataBuffer = new ComputeBuffer(vertexCount * segmentCount, 4 * sizeof(float));

            try {
                vertexBuffer.SetData(vertices);
                normalBuffer.SetData(normals);
                maskBuffer.SetData(mask);
                splineBuffer.SetData(gpuSpline);
                dataBuffer.SetData(data);
                highlightBuffer.SetData(sectionHighlights);
                segmentBuffer.SetData(segments);

                int kernel = _computeShader.FindKernel("DeformKernel");
                _computeShader.SetBuffer(kernel, "_SourceVertices", vertexBuffer);
                _computeShader.SetBuffer(kernel, "_SourceNormals", normalBuffer);
                _computeShader.SetBuffer(kernel, "_SourceMask", maskBuffer);
                _computeShader.SetBuffer(kernel, "_SplinePoints", splineBuffer);
                _computeShader.SetBuffer(kernel, "_SourceData", dataBuffer);
                _computeShader.SetBuffer(kernel, "_SectionHighlights", highlightBuffer);
                _computeShader.SetBuffer(kernel, "_Segments", segmentBuffer);
                _computeShader.SetBuffer(kernel, "_OutputVertices", outputVertexBuffer);
                _computeShader.SetBuffer(kernel, "_OutputNormals", outputNormalBuffer);
                _computeShader.SetBuffer(kernel, "_OutputMask", outputMaskBuffer);
                _computeShader.SetBuffer(kernel, "_OutputData", outputDataBuffer);
                _computeShader.SetInt("_VertexCount", vertexCount);
                _computeShader.SetInt("_SegmentCount", segmentCount);
                _computeShader.SetFloat("_NominalLength", NOMINAL_LENGTH);

                int totalVerts = vertexCount * segmentCount;
                int threadGroups = (int)math.ceil(totalVerts / 64f);
                _computeShader.Dispatch(kernel, threadGroups, 1, 1);

                var readback = AsyncGPUReadback.Request(outputVertexBuffer);
                yield return new WaitUntil(() => readback.done);

                if (readback.hasError) {
                    Assert.Fail("GPU readback failed");
                    yield break;
                }

                var gpuVertices = readback.GetData<float3>();

                float seg0StartZ = (gpuVertices[0].z + gpuVertices[1].z) / 2f;
                float seg0EndZ = (gpuVertices[2].z + gpuVertices[3].z) / 2f;
                Assert.AreEqual(0f, seg0StartZ, TOLERANCE, "Segment 0 start Z");
                Assert.AreEqual(-10f, seg0EndZ, TOLERANCE, "Segment 0 end Z");

                int seg1Offset = vertexCount;
                float seg1StartZ = (gpuVertices[seg1Offset + 0].z + gpuVertices[seg1Offset + 1].z) / 2f;
                float seg1EndZ = (gpuVertices[seg1Offset + 2].z + gpuVertices[seg1Offset + 3].z) / 2f;
                Assert.AreEqual(-10f, seg1StartZ, TOLERANCE, "Segment 1 start Z");
                Assert.AreEqual(-20f, seg1EndZ, TOLERANCE, "Segment 1 end Z");

                int seg2Offset = 2 * vertexCount;
                float seg2StartZ = (gpuVertices[seg2Offset + 0].z + gpuVertices[seg2Offset + 1].z) / 2f;
                float seg2EndZ = (gpuVertices[seg2Offset + 2].z + gpuVertices[seg2Offset + 3].z) / 2f;
                Assert.AreEqual(-20f, seg2StartZ, TOLERANCE, "Segment 2 start Z");
                Assert.AreEqual(-30f, seg2EndZ, TOLERANCE, "Segment 2 end Z");
            }
            finally {
                vertexBuffer.Dispose();
                normalBuffer.Dispose();
                maskBuffer.Dispose();
                splineBuffer.Dispose();
                dataBuffer.Dispose();
                highlightBuffer.Dispose();
                segmentBuffer.Dispose();
                outputVertexBuffer.Dispose();
                outputNormalBuffer.Dispose();
                outputMaskBuffer.Dispose();
                outputDataBuffer.Dispose();
                vertices.Dispose();
                normals.Dispose();
                mask.Dispose();
                gpuSpline.Dispose();
                data.Dispose();
                sectionHighlights.Dispose();
                segments.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Kernel_SectionHighlights_OutputsCorrectHighlightValues() {
            if (_computeShader == null) {
                Assert.Ignore("Compute shader not found - skipping GPU test");
                yield break;
            }

            var vertices = CreateQuadVertices(Allocator.Persistent);
            var normals = CreateQuadNormals(Allocator.Persistent);
            var mask = CreateDefaultMask(4, Allocator.Persistent);
            var gpuSpline = CreateStraightGPUSpline(40f, Allocator.Persistent);
            var data = CreateDefaultData(gpuSpline.Length, Allocator.Persistent);

            int vertexCount = vertices.Length;
            int segmentCount = 3;

            // Create 3 segments with different section indices
            var segments = new NativeArray<GPUSegmentBoundary>(segmentCount, Allocator.Persistent);
            segments[0] = new GPUSegmentBoundary { StartArc = 0f, Length = 10f, Scale = 1f, ArcStart = 0f, ArcLength = 40f, SplineStartIndex = 0, SplineCount = gpuSpline.Length, PieceIndex = 0, SectionIndex = 0 };
            segments[1] = new GPUSegmentBoundary { StartArc = 10f, Length = 10f, Scale = 1f, ArcStart = 0f, ArcLength = 40f, SplineStartIndex = 0, SplineCount = gpuSpline.Length, PieceIndex = 0, SectionIndex = 1 };
            segments[2] = new GPUSegmentBoundary { StartArc = 20f, Length = 10f, Scale = 1f, ArcStart = 0f, ArcLength = 40f, SplineStartIndex = 0, SplineCount = gpuSpline.Length, PieceIndex = 0, SectionIndex = 2 };

            // Set different highlight values for each section
            var sectionHighlights = new NativeArray<float>(3, Allocator.Persistent);
            sectionHighlights[0] = 0.0f;  // Section 0: not highlighted
            sectionHighlights[1] = 0.5f;  // Section 1: half highlighted
            sectionHighlights[2] = 1.0f;  // Section 2: fully highlighted

            var vertexBuffer = new ComputeBuffer(vertexCount, 3 * sizeof(float));
            var normalBuffer = new ComputeBuffer(vertexCount, 3 * sizeof(float));
            var maskBuffer = new ComputeBuffer(vertexCount, 4 * sizeof(float));
            var splineBuffer = new ComputeBuffer(gpuSpline.Length, GPUSplinePoint.Stride);
            var dataBuffer = new ComputeBuffer(gpuSpline.Length, sizeof(float));
            var highlightBuffer = new ComputeBuffer(3, sizeof(float));
            var segmentBuffer = new ComputeBuffer(segmentCount, GPUSegmentBoundary.Stride);
            var outputVertexBuffer = new ComputeBuffer(vertexCount * segmentCount, 3 * sizeof(float));
            var outputNormalBuffer = new ComputeBuffer(vertexCount * segmentCount, 3 * sizeof(float));
            var outputMaskBuffer = new ComputeBuffer(vertexCount * segmentCount, 4 * sizeof(float));
            var outputDataBuffer = new ComputeBuffer(vertexCount * segmentCount, 4 * sizeof(float));

            try {
                vertexBuffer.SetData(vertices);
                normalBuffer.SetData(normals);
                maskBuffer.SetData(mask);
                splineBuffer.SetData(gpuSpline);
                dataBuffer.SetData(data);
                highlightBuffer.SetData(sectionHighlights);
                segmentBuffer.SetData(segments);

                int kernel = _computeShader.FindKernel("DeformKernel");
                _computeShader.SetBuffer(kernel, "_SourceVertices", vertexBuffer);
                _computeShader.SetBuffer(kernel, "_SourceNormals", normalBuffer);
                _computeShader.SetBuffer(kernel, "_SourceMask", maskBuffer);
                _computeShader.SetBuffer(kernel, "_SplinePoints", splineBuffer);
                _computeShader.SetBuffer(kernel, "_SourceData", dataBuffer);
                _computeShader.SetBuffer(kernel, "_SectionHighlights", highlightBuffer);
                _computeShader.SetBuffer(kernel, "_Segments", segmentBuffer);
                _computeShader.SetBuffer(kernel, "_OutputVertices", outputVertexBuffer);
                _computeShader.SetBuffer(kernel, "_OutputNormals", outputNormalBuffer);
                _computeShader.SetBuffer(kernel, "_OutputMask", outputMaskBuffer);
                _computeShader.SetBuffer(kernel, "_OutputData", outputDataBuffer);
                _computeShader.SetInt("_VertexCount", vertexCount);
                _computeShader.SetInt("_SegmentCount", segmentCount);
                _computeShader.SetFloat("_NominalLength", NOMINAL_LENGTH);

                int totalVerts = vertexCount * segmentCount;
                int threadGroups = (int)math.ceil(totalVerts / 64f);
                _computeShader.Dispatch(kernel, threadGroups, 1, 1);

                var readback = AsyncGPUReadback.Request(outputDataBuffer);
                yield return new WaitUntil(() => readback.done);

                if (readback.hasError) {
                    Assert.Fail("GPU readback failed");
                    yield break;
                }

                var gpuData = readback.GetData<float4>();

                // Verify highlight values (Data.y) for each segment
                // Segment 0 (vertices 0-3) should have highlight = 0.0
                for (int v = 0; v < vertexCount; v++) {
                    Assert.AreEqual(0.0f, gpuData[v].y, TOLERANCE,
                        $"Segment 0, Vertex {v}: highlight should be 0.0");
                }

                // Segment 1 (vertices 4-7) should have highlight = 0.5
                int seg1Offset = vertexCount;
                for (int v = 0; v < vertexCount; v++) {
                    Assert.AreEqual(0.5f, gpuData[seg1Offset + v].y, TOLERANCE,
                        $"Segment 1, Vertex {v}: highlight should be 0.5");
                }

                // Segment 2 (vertices 8-11) should have highlight = 1.0
                int seg2Offset = 2 * vertexCount;
                for (int v = 0; v < vertexCount; v++) {
                    Assert.AreEqual(1.0f, gpuData[seg2Offset + v].y, TOLERANCE,
                        $"Segment 2, Vertex {v}: highlight should be 1.0");
                }
            }
            finally {
                vertexBuffer.Dispose();
                normalBuffer.Dispose();
                maskBuffer.Dispose();
                splineBuffer.Dispose();
                dataBuffer.Dispose();
                highlightBuffer.Dispose();
                segmentBuffer.Dispose();
                outputVertexBuffer.Dispose();
                outputNormalBuffer.Dispose();
                outputMaskBuffer.Dispose();
                outputDataBuffer.Dispose();
                vertices.Dispose();
                normals.Dispose();
                mask.Dispose();
                gpuSpline.Dispose();
                data.Dispose();
                sectionHighlights.Dispose();
                segments.Dispose();
            }
        }
    }
}
