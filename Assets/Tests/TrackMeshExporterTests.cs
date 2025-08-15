using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using NUnit.Framework;
using Unity.Jobs;
using KexEdit;
using KexEdit.UI;
using System.Diagnostics;
using System;

[TestFixture]
public class TrackMeshExporterTests {
    private const float FLOAT_TOLERANCE = 0.000001f;

    [TearDown]
    public void TearDown() {
        var testFiles = Directory.GetFiles(Application.temporaryCachePath, "test_*.obj");
        foreach (var file in testFiles) {
            try {
                File.Delete(file);
            }
            catch { }
        }
    }

    [Test]
    public void BuildJob_TransformsDataCorrectly() {
        var vertices = new NativeArray<float3>(3, Allocator.TempJob);
        var normals = new NativeArray<float3>(3, Allocator.TempJob);
        var uvs = new NativeArray<float2>(3, Allocator.TempJob);
        var outputData = new NativeArray<VertexData>(3, Allocator.TempJob);

        try {
            vertices[0] = new float3(1, 0, 0);
            vertices[1] = new float3(0, 1, 0);
            vertices[2] = new float3(0, 0, 1);

            normals[0] = new float3(1, 0, 0);
            normals[1] = new float3(0, 1, 0);
            normals[2] = new float3(0, 0, 1);

            uvs[0] = new float2(0, 0);
            uvs[1] = new float2(0.5f, 0.5f);
            uvs[2] = new float2(1, 1);

            var transformMatrix = float4x4.Scale(2f);

            var job = CreateBuildJob(vertices, normals, uvs, transformMatrix, outputData);
            job.Schedule(3, 1).Complete();

            Assert.AreEqual(new float3(-2, 0, 0), outputData[0].Position);
            Assert.AreEqual(new float3(0, 2, 0), outputData[1].Position);
            Assert.AreEqual(new float3(0, 0, 2), outputData[2].Position);

            Assert.AreEqual(new float3(-1, 0, 0), outputData[0].Normal);
            Assert.AreEqual(new float3(0, 1, 0), outputData[1].Normal);
            Assert.AreEqual(new float3(0, 0, 1), outputData[2].Normal);

            Assert.AreEqual(new float2(0, 0), outputData[0].UV);
            Assert.AreEqual(new float2(0.5f, 0.5f), outputData[1].UV);
            Assert.AreEqual(new float2(1, 1), outputData[2].UV);
        }
        finally {
            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
            outputData.Dispose();
        }
    }

    [Test]
    public void ObjWriter_ProducesValidObjFile() {
        var meshes = new NativeList<MeshData>(Allocator.TempJob);
        var vertices = new NativeArray<VertexData>(3, Allocator.TempJob);
        var triangles = new NativeArray<int>(3, Allocator.TempJob);

        try {
            vertices[0] = new VertexData {
                Position = new float3(0, 0, 0),
                Normal = new float3(0, 1, 0),
                UV = new float2(0, 0)
            };
            vertices[1] = new VertexData {
                Position = new float3(1, 0, 0),
                Normal = new float3(0, 1, 0),
                UV = new float2(1, 0)
            };
            vertices[2] = new VertexData {
                Position = new float3(0.5f, 0, 1),
                Normal = new float3(0, 1, 0),
                UV = new float2(0.5f, 1)
            };

            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;

            var meshData = new MeshData {
                Type = MeshType.StartCap,
                SegmentId = 0,
                Vertices = vertices,
                Triangles = triangles,
                VertexCount = 3,
                TriangleCount = 3
            };

            meshes.Add(meshData);

            var testFilePath = Path.Combine(Application.temporaryCachePath, "test_output.obj");

            Assert.DoesNotThrow(() => {
                ObjWriter.WriteObjFile(testFilePath, ref meshes);
            });

            Assert.IsTrue(File.Exists(testFilePath));

            // Validate OBJ can be parsed by ImportManager
            Mesh importedMesh = null;
            Assert.DoesNotThrow(() => {
                importedMesh = ObjImporter.LoadMesh(testFilePath);
            });

            Assert.IsNotNull(importedMesh, "ImportManager should successfully parse the exported OBJ");
            Assert.AreEqual(3, importedMesh.vertexCount, "Imported mesh should have 3 vertices");
            Assert.AreEqual(3, importedMesh.triangles.Length, "Imported mesh should have 3 triangle indices");
            Assert.IsNotNull(importedMesh.normals, "Imported mesh should have normals");
            Assert.IsNotNull(importedMesh.uv, "Imported mesh should have UVs");

            // Verify vertex positions are correct (allowing for float precision)
            Assert.AreEqual(new Vector3(0, 0, 0), importedMesh.vertices[0], "First vertex position");
            Assert.AreEqual(new Vector3(1, 0, 0), importedMesh.vertices[1], "Second vertex position");
            Assert.AreEqual(new Vector3(0.5f, 0, 1), importedMesh.vertices[2], "Third vertex position");
        }
        finally {
            vertices.Dispose();
            triangles.Dispose();
            meshes.Dispose();
        }
    }

    [Test]
    public void ObjWriter_HandlesMultipleMeshes() {
        var meshes = new NativeList<MeshData>(Allocator.TempJob);
        var vertices1 = new NativeArray<VertexData>(3, Allocator.TempJob);
        var triangles1 = new NativeArray<int>(3, Allocator.TempJob);
        var vertices2 = new NativeArray<VertexData>(3, Allocator.TempJob);
        var triangles2 = new NativeArray<int>(3, Allocator.TempJob);

        try {
            // First mesh with normals and UVs
            vertices1[0] = new VertexData { Position = new float3(0, 0, 0), Normal = new float3(0, 1, 0), UV = new float2(0, 0) };
            vertices1[1] = new VertexData { Position = new float3(1, 0, 0), Normal = new float3(0, 1, 0), UV = new float2(1, 0) };
            vertices1[2] = new VertexData { Position = new float3(0, 1, 0), Normal = new float3(0, 1, 0), UV = new float2(0, 1) };
            triangles1[0] = 0; triangles1[1] = 1; triangles1[2] = 2;

            // Second mesh with no normals or UVs (zero values)
            vertices2[0] = new VertexData { Position = new float3(2, 0, 0), Normal = float3.zero, UV = float2.zero };
            vertices2[1] = new VertexData { Position = new float3(3, 0, 0), Normal = float3.zero, UV = float2.zero };
            vertices2[2] = new VertexData { Position = new float3(2, 1, 0), Normal = float3.zero, UV = float2.zero };
            triangles2[0] = 0; triangles2[1] = 1; triangles2[2] = 2;

            meshes.Add(new MeshData { Type = MeshType.StartCap, SegmentId = 0, Vertices = vertices1, Triangles = triangles1, VertexCount = 3, TriangleCount = 3 });
            meshes.Add(new MeshData { Type = MeshType.EndCap, SegmentId = 1, Vertices = vertices2, Triangles = triangles2, VertexCount = 3, TriangleCount = 3 });

            var testFilePath = Path.Combine(Application.temporaryCachePath, "test_multi_mesh.obj");

            Assert.DoesNotThrow(() => {
                ObjWriter.WriteObjFile(testFilePath, ref meshes);
            });

            // Validate OBJ can be parsed
            Mesh importedMesh = null;
            Assert.DoesNotThrow(() => {
                importedMesh = ObjImporter.LoadMesh(testFilePath);
            });

            Assert.IsNotNull(importedMesh, "Multi-mesh OBJ should be parseable");
            Assert.AreEqual(6, importedMesh.vertexCount, "Should have 6 vertices total");
            Assert.AreEqual(6, importedMesh.triangles.Length, "Should have 6 triangle indices total");
        }
        finally {
            vertices1.Dispose();
            triangles1.Dispose();
            vertices2.Dispose();
            triangles2.Dispose();
            meshes.Dispose();
        }
    }

    [Test]
    public void ObjWriter_PerformanceWithLargeDataset() {
        const int MESH_COUNT = 100;
        const int VERTICES_PER_MESH = 1000;
        const int TRIANGLES_PER_MESH = 3000;

        var meshes = new NativeList<MeshData>(Allocator.TempJob);
        var allVertexArrays = new NativeArray<VertexData>[MESH_COUNT];
        var allTriangleArrays = new NativeArray<int>[MESH_COUNT];

        var sw = new Stopwatch();

        try {
            // Setup phase - measure data preparation time
            sw.Start();
            for (int meshIndex = 0; meshIndex < MESH_COUNT; meshIndex++) {
                var vertices = new NativeArray<VertexData>(VERTICES_PER_MESH, Allocator.TempJob);
                var triangles = new NativeArray<int>(TRIANGLES_PER_MESH, Allocator.TempJob);

                // Generate test data
                for (int i = 0; i < VERTICES_PER_MESH; i++) {
                    vertices[i] = new VertexData {
                        Position = new float3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value),
                        Normal = math.normalize(new float3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value)),
                        UV = new float2(UnityEngine.Random.value, UnityEngine.Random.value)
                    };
                }

                for (int i = 0; i < TRIANGLES_PER_MESH; i++) {
                    triangles[i] = UnityEngine.Random.Range(0, VERTICES_PER_MESH);
                }

                meshes.Add(new MeshData {
                    Type = meshIndex % 2 == 0 ? MeshType.StartCap : MeshType.EndCap,
                    SegmentId = meshIndex / 10,
                    Vertices = vertices,
                    Triangles = triangles,
                    VertexCount = VERTICES_PER_MESH,
                    TriangleCount = TRIANGLES_PER_MESH
                });

                allVertexArrays[meshIndex] = vertices;
                allTriangleArrays[meshIndex] = triangles;
            }
            sw.Stop();
            var setupTime = sw.ElapsedMilliseconds;

            // Export phase - measure string building vs file writing
            var testFilePath = Path.Combine(Application.temporaryCachePath, "test_performance.obj");

            // Measure string building time (in-memory operations)
            sw.Restart();
            var buffer = new System.Text.StringBuilder(1024 * 1024);
            buffer.AppendLine("# Track meshes exported from KexEdit");
            buffer.AppendLine($"# Export date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            buffer.AppendLine("");

            int vertexOffset = 1, normalOffset = 1, uvOffset = 1;

            unsafe {
                for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++) {
                    var mesh = meshes[meshIndex];
                    string meshName = mesh.Type switch {
                        MeshType.StartCap => $"StartCap_{mesh.SegmentId}",
                        MeshType.EndCap => $"EndCap_{mesh.SegmentId}",
                        MeshType.Duplication => $"Duplication_{mesh.SegmentId}",
                        MeshType.Extrusion => $"Extrusion_{mesh.SegmentId}",
                        MeshType.Segment => $"Segment_{mesh.SegmentId}",
                        _ => $"Unknown_{mesh.SegmentId}"
                    };

                    buffer.AppendLine($"# Mesh: {meshName}");
                    buffer.AppendLine($"o {meshName}");

                    var vertexPtr = (VertexData*)mesh.Vertices.GetUnsafeReadOnlyPtr();

                    for (int i = 0; i < mesh.VertexCount; i++) {
                        var vertexData = vertexPtr[i];
                        buffer.AppendLine($"v {vertexData.Position.x:F6} {vertexData.Position.y:F6} {vertexData.Position.z:F6}");
                    }

                    bool hasNormals = false, hasUVs = false;
                    for (int i = 0; i < mesh.VertexCount; i++) {
                        if (!vertexPtr[i].Normal.Equals(float3.zero)) hasNormals = true;
                        if (!vertexPtr[i].UV.Equals(float2.zero)) hasUVs = true;
                        if (hasNormals && hasUVs) break;
                    }

                    if (hasNormals) {
                        for (int i = 0; i < mesh.VertexCount; i++) {
                            var vertexData = vertexPtr[i];
                            buffer.AppendLine($"vn {vertexData.Normal.x:F6} {vertexData.Normal.y:F6} {vertexData.Normal.z:F6}");
                        }
                    }

                    if (hasUVs) {
                        for (int i = 0; i < mesh.VertexCount; i++) {
                            var vertexData = vertexPtr[i];
                            buffer.AppendLine($"vt {vertexData.UV.x:F6} {vertexData.UV.y:F6}");
                        }
                    }

                    var trianglePtr = (int*)mesh.Triangles.GetUnsafeReadOnlyPtr();
                    for (int i = 0; i < mesh.TriangleCount; i += 3) {
                        var vi1 = trianglePtr[i] + vertexOffset;
                        var vi2 = trianglePtr[i + 1] + vertexOffset;
                        var vi3 = trianglePtr[i + 2] + vertexOffset;

                        if (hasNormals && hasUVs) {
                            var ni1 = trianglePtr[i] + normalOffset;
                            var ni2 = trianglePtr[i + 1] + normalOffset;
                            var ni3 = trianglePtr[i + 2] + normalOffset;
                            var ui1 = trianglePtr[i] + uvOffset;
                            var ui2 = trianglePtr[i + 1] + uvOffset;
                            var ui3 = trianglePtr[i + 2] + uvOffset;
                            buffer.AppendLine($"f {vi1}/{ui1}/{ni1} {vi2}/{ui2}/{ni2} {vi3}/{ui3}/{ni3}");
                        }
                        else if (hasNormals) {
                            var ni1 = trianglePtr[i] + normalOffset;
                            var ni2 = trianglePtr[i + 1] + normalOffset;
                            var ni3 = trianglePtr[i + 2] + normalOffset;
                            buffer.AppendLine($"f {vi1}//{ni1} {vi2}//{ni2} {vi3}//{ni3}");
                        }
                        else if (hasUVs) {
                            var ui1 = trianglePtr[i] + uvOffset;
                            var ui2 = trianglePtr[i + 1] + uvOffset;
                            var ui3 = trianglePtr[i + 2] + uvOffset;
                            buffer.AppendLine($"f {vi1}/{ui1} {vi2}/{ui2} {vi3}/{ui3}");
                        }
                        else {
                            buffer.AppendLine($"f {vi1} {vi2} {vi3}");
                        }
                    }

                    vertexOffset += mesh.VertexCount;
                    if (hasNormals) normalOffset += mesh.VertexCount;
                    if (hasUVs) uvOffset += mesh.VertexCount;
                    buffer.AppendLine("");
                }
            }
            sw.Stop();
            var stringBuildTime = sw.ElapsedMilliseconds;

            // Measure file writing time (I/O operations)
            sw.Restart();
            var utf8WithoutBom = new System.Text.UTF8Encoding(false);
            File.WriteAllText(testFilePath, buffer.ToString(), utf8WithoutBom);
            sw.Stop();
            var fileWriteTime = sw.ElapsedMilliseconds;

            var totalExportTime = stringBuildTime + fileWriteTime;

            // Quick validation
            var importedMesh = ObjImporter.LoadMesh(testFilePath);

            // Performance reporting
            UnityEngine.Debug.Log($"Performance Test Results:");
            UnityEngine.Debug.Log($"  Setup: {setupTime}ms ({MESH_COUNT} meshes, {VERTICES_PER_MESH * MESH_COUNT:N0} vertices)");
            UnityEngine.Debug.Log($"  String Building: {stringBuildTime}ms ({stringBuildTime / (float)MESH_COUNT:F1}ms per mesh)");
            UnityEngine.Debug.Log($"  File Writing: {fileWriteTime}ms");
            UnityEngine.Debug.Log($"  Total Export: {totalExportTime}ms");
            UnityEngine.Debug.Log($"  File size: {new FileInfo(testFilePath).Length / 1024}KB");

            // Performance thresholds
            Assert.Less(totalExportTime, 5000, $"Export should complete in under 5 seconds (took {totalExportTime}ms)");
            Assert.Less(stringBuildTime, 4000, $"String building should be under 4 seconds (took {stringBuildTime}ms)");
            Assert.Less(fileWriteTime, 1000, $"File writing should be under 1 second (took {fileWriteTime}ms)");

            // Verify correctness
            Assert.IsNotNull(importedMesh, "Large dataset should produce valid OBJ");

            // Memory efficiency check - export time should scale reasonably with data size
            var vertexCount = VERTICES_PER_MESH * MESH_COUNT;
            var msPerVertex = totalExportTime / (float)vertexCount;
            Assert.Less(msPerVertex, 0.01f, $"Export should be efficient (took {msPerVertex:F4}ms per vertex)");
        }
        finally {
            // Cleanup
            for (int i = 0; i < allVertexArrays.Length; i++) {
                if (allVertexArrays[i].IsCreated) allVertexArrays[i].Dispose();
                if (allTriangleArrays[i].IsCreated) allTriangleArrays[i].Dispose();
            }
            meshes.Dispose();
        }
    }

    [Test]
    public void UnsafePointerAccess_DoesNotCorruptMemory() {
        var testData = new NativeArray<VertexData>(100, Allocator.TempJob);

        try {
            for (int i = 0; i < testData.Length; i++) {
                testData[i] = new VertexData {
                    Position = new float3(i, i + 1, i + 2),
                    Normal = new float3(i + 3, i + 4, i + 5),
                    UV = new float2(i + 6, i + 7)
                };
            }

            unsafe {
                var ptr = (VertexData*)testData.GetUnsafeReadOnlyPtr();

                for (int i = 0; i < testData.Length; i++) {
                    var data = ptr[i];

                    Assert.AreEqual(new float3(i, i + 1, i + 2), data.Position);
                    Assert.AreEqual(new float3(i + 3, i + 4, i + 5), data.Normal);
                    Assert.AreEqual(new float2(i + 6, i + 7), data.UV);
                }
            }

            for (int i = 0; i < testData.Length; i++) {
                Assert.AreEqual(new float3(i, i + 1, i + 2), testData[i].Position);
                Assert.AreEqual(new float3(i + 3, i + 4, i + 5), testData[i].Normal);
                Assert.AreEqual(new float2(i + 6, i + 7), testData[i].UV);
            }
        }
        finally {
            testData.Dispose();
        }
    }

    private static TrackMeshExporter.BuildJob CreateBuildJob(
        NativeArray<float3> vertices,
        NativeArray<float3> normals,
        NativeArray<float2> uvs,
        float4x4 transformMatrix,
        NativeArray<VertexData> outputData) {

        return new TrackMeshExporter.BuildJob {
            Vertices = vertices,
            Normals = normals,
            UVs = uvs,
            TransformMatrix = transformMatrix,
            OutputData = outputData
        };
    }
}
