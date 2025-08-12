using System;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using SFB;
using Unity.Burst;
using System.Runtime.InteropServices;

namespace KexEdit.UI {
    [BurstCompile]
    public static class TrackMeshExporter {
        public static void ExportTrackMesh() {
            try {
                string filePath = StandaloneFileBrowser.SaveFilePanel(
                    "Export Track Mesh",
                    Application.persistentDataPath,
                    "track",
                    new[] {
                        new ExtensionFilter("OBJ", "obj"),
                        new ExtensionFilter("All Files", "*")
                    });

                if (!string.IsNullOrEmpty(filePath)) {
                    ExportTrackMeshInternal(filePath);
                }
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to export track mesh: {ex.Message}");
            }
        }

        private static void ExportTrackMeshInternal(string filePath) {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var trackStyleQuery = entityManager.CreateEntityQuery(typeof(Segment), typeof(SectionReference));

            if (trackStyleQuery.IsEmpty) {
                Debug.LogWarning("No track sections found to export");
                return;
            }

            var entities = trackStyleQuery.ToEntityArray(Allocator.Temp);
            var meshes = new NativeList<MeshData>(Allocator.TempJob);

            try {
                for (int segmentIndex = 0; segmentIndex < entities.Length; segmentIndex++) {
                    var entity = entities[segmentIndex];
                    var segment = entityManager.GetComponentData<Segment>(entity);
                    if (!segment.HasBuffers) continue;

                    var sectionReference = entityManager.GetComponentData<SectionReference>(entity);

                    if (!entityManager.HasComponent<RenderedStyleHash>(sectionReference.Value) ||
                        segment.StyleHash != entityManager.GetComponentData<RenderedStyleHash>(sectionReference.Value)) {
                        continue;
                    }

                    var buffers = entityManager.GetComponentObject<TrackStyleBuffers>(entity);
                    ExtractAndCombineSegmentMeshes(buffers.CurrentBuffers, segmentIndex, ref meshes);
                }

                entities.Dispose();

                if (meshes.Length == 0) {
                    Debug.LogWarning("No meshes found to export");
                    return;
                }

                ObjWriter.WriteObjFile(filePath, ref meshes);
                Debug.Log($"Exported {meshes.Length} meshes to: {filePath}");
            }
            finally {
                DisposeMeshData(ref meshes);
            }
        }

        private static void ExtractAndCombineSegmentMeshes(MeshBuffers meshBuffers, int segmentId, ref NativeList<MeshData> meshes) {
            var segmentMeshes = new NativeList<MeshData>(Allocator.Temp);

            try {
                for (int i = 0; i < meshBuffers.StartCapBuffers.Count; i++) {
                    var startCapBuffer = meshBuffers.StartCapBuffers[i];
                    ExtractCapBuffer(startCapBuffer, i, true, segmentId, ref segmentMeshes);
                }

                for (int i = 0; i < meshBuffers.EndCapBuffers.Count; i++) {
                    var endCapBuffer = meshBuffers.EndCapBuffers[i];
                    ExtractCapBuffer(endCapBuffer, i, false, segmentId, ref segmentMeshes);
                }

                for (int i = 0; i < meshBuffers.DuplicationBuffers.Count; i++) {
                    var duplicationBuffer = meshBuffers.DuplicationBuffers[i];
                    ExtractDuplicationBuffer(duplicationBuffer, i, segmentId, ref segmentMeshes);
                }

                for (int i = 0; i < meshBuffers.ExtrusionBuffers.Count; i++) {
                    var extrusionBuffer = meshBuffers.ExtrusionBuffers[i];
                    ExtractExtrusionBuffer(extrusionBuffer, i, segmentId, ref segmentMeshes);
                }

                if (segmentMeshes.Length > 0) {
                    var combinedMesh = CombineSegmentMeshes(ref segmentMeshes, segmentId);
                    meshes.Add(combinedMesh);
                }
            }
            finally {
                DisposeMeshData(ref segmentMeshes);
            }
        }

        private static void ExtractCapBuffer(CapMeshBuffers capBuffer, int index, bool isStartCap, int segmentId, ref NativeList<MeshData> meshes) {
            if (capBuffer.Mesh == null) return;

            var matrixReadback = AsyncGPUReadback.Request(capBuffer.MatricesBuffer);
            matrixReadback.WaitForCompletion();

            if (matrixReadback.hasError) return;

            var matrices = matrixReadback.GetData<float4x4>();
            var sourceMesh = capBuffer.Mesh;

            var vertices = new NativeArray<float3>(sourceMesh.vertices.Length, Allocator.TempJob);
            var normals = new NativeArray<float3>(sourceMesh.normals.Length, Allocator.TempJob);
            var uvs = new NativeArray<float2>(sourceMesh.uv.Length, Allocator.TempJob);
            var triangles = new NativeArray<int>(sourceMesh.triangles.Length, Allocator.TempJob);

            CopyVertices(sourceMesh.vertices, vertices);
            CopyNormals(sourceMesh.normals, normals);
            CopyUVs(sourceMesh.uv, uvs);
            CopyTriangles(sourceMesh.triangles, triangles);

            for (int matrixIndex = 0; matrixIndex < matrices.Length; matrixIndex++) {
                var meshVertices = new NativeArray<VertexData>(vertices.Length, Allocator.TempJob);

                var buildJob = new BuildJob {
                    Vertices = vertices,
                    Normals = normals,
                    UVs = uvs,
                    TransformMatrix = matrices[matrixIndex],
                    OutputData = meshVertices
                };

                buildJob.Schedule(vertices.Length, 64).Complete();

                var meshData = new MeshData {
                    Type = isStartCap ? MeshType.StartCap : MeshType.EndCap,
                    SegmentId = segmentId,
                    Vertices = meshVertices,
                    Triangles = triangles,
                    VertexCount = vertices.Length,
                    TriangleCount = triangles.Length
                };
                meshes.Add(meshData);
            }

            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
        }

        private static void ExtractDuplicationBuffer(
            DuplicationMeshBuffers duplicationBuffer, 
            int index, 
            int segmentId, 
            ref NativeList<MeshData> meshes
        ) {
            if (duplicationBuffer.Mesh == null) return;

            var matrixReadback = AsyncGPUReadback.Request(duplicationBuffer.MatricesBuffer);
            matrixReadback.WaitForCompletion();

            if (matrixReadback.hasError) return;

            var matrices = matrixReadback.GetData<float4x4>();
            var sourceMesh = duplicationBuffer.Mesh;

            if (matrices.Length == 0) return;

            var vertices = new NativeArray<float3>(sourceMesh.vertices.Length, Allocator.TempJob);
            var normals = new NativeArray<float3>(sourceMesh.normals.Length, Allocator.TempJob);
            var uvs = new NativeArray<float2>(sourceMesh.uv.Length, Allocator.TempJob);

            CopyVertices(sourceMesh.vertices, vertices);
            CopyNormals(sourceMesh.normals, normals);
            CopyUVs(sourceMesh.uv, uvs);

            var individualMeshes = new NativeList<MeshData>(matrices.Length, Allocator.Temp);

            try {
                for (int matrixIndex = 0; matrixIndex < matrices.Length; matrixIndex++) {
                    var meshVertices = new NativeArray<VertexData>(vertices.Length, Allocator.TempJob);
                    var meshTriangles = new NativeArray<int>(sourceMesh.triangles.Length, Allocator.TempJob);

                    CopyTriangles(sourceMesh.triangles, meshTriangles);

                    var buildJob = new BuildJob {
                        Vertices = vertices,
                        Normals = normals,
                        UVs = uvs,
                        TransformMatrix = matrices[matrixIndex],
                        OutputData = meshVertices
                    };

                    buildJob.Schedule(vertices.Length, 64).Complete();

                    var meshData = new MeshData {
                        Type = MeshType.Duplication,
                        SegmentId = segmentId,
                        Vertices = meshVertices,
                        Triangles = meshTriangles,
                        VertexCount = vertices.Length,
                        TriangleCount = meshTriangles.Length
                    };
                    individualMeshes.Add(meshData);
                }

                int totalVertexCount = vertices.Length * matrices.Length;
                int totalTriangleCount = sourceMesh.triangles.Length * matrices.Length;

                var combinedVertices = new NativeArray<VertexData>(totalVertexCount, Allocator.TempJob);
                var combinedTriangles = new NativeArray<int>(totalTriangleCount, Allocator.TempJob);

                int vertexOffset = 0;
                int triangleOffset = 0;

                for (int i = 0; i < individualMeshes.Length; i++) {
                    var mesh = individualMeshes[i];

                    NativeArray<VertexData>.Copy(mesh.Vertices, 0, combinedVertices, vertexOffset, mesh.VertexCount);
                    NativeArray<int>.Copy(mesh.Triangles, 0, combinedTriangles, triangleOffset, mesh.TriangleCount);

                    for (int t = 0; t < mesh.TriangleCount; t++) {
                        combinedTriangles[triangleOffset + t] += vertexOffset;
                    }

                    vertexOffset += mesh.VertexCount;
                    triangleOffset += mesh.TriangleCount;

                    mesh.Vertices.Dispose();
                    mesh.Triangles.Dispose();
                }

                var finalMeshData = new MeshData {
                    Type = MeshType.Duplication,
                    SegmentId = segmentId,
                    Vertices = combinedVertices,
                    Triangles = combinedTriangles,
                    VertexCount = totalVertexCount,
                    TriangleCount = totalTriangleCount
                };

                meshes.Add(finalMeshData);
            }
            finally {
                individualMeshes.Dispose();
                vertices.Dispose();
                normals.Dispose();
                uvs.Dispose();
            }
        }

        private static void ExtractExtrusionBuffer(ExtrusionMeshBuffers extrusionBuffer, int index, int segmentId, ref NativeList<MeshData> meshes) {
            var verticesReadback = AsyncGPUReadback.Request(extrusionBuffer.ExtrusionVerticesBuffer);
            var normalsReadback = AsyncGPUReadback.Request(extrusionBuffer.ExtrusionNormalsBuffer);
            var indicesReadback = AsyncGPUReadback.Request(extrusionBuffer.ExtrusionIndicesBuffer);
            var uvsReadback = AsyncGPUReadback.Request(extrusionBuffer.CrossSectionUVsBuffer);

            verticesReadback.WaitForCompletion();
            normalsReadback.WaitForCompletion();
            indicesReadback.WaitForCompletion();
            uvsReadback.WaitForCompletion();

            if (verticesReadback.hasError || normalsReadback.hasError || indicesReadback.hasError || uvsReadback.hasError) return;

            var vertices = verticesReadback.GetData<Vector3>();
            var normals = normalsReadback.GetData<Vector3>();
            var indices = indicesReadback.GetData<uint>();
            var uvs = uvsReadback.GetData<Vector2>();

            int vertexCount = vertices.Length;
            int triangleCount = indices.Length;
            int crossSectionVertexCount = extrusionBuffer.CrossSectionVerticesBuffer.count;

            var meshVertices = new NativeArray<VertexData>(vertexCount, Allocator.TempJob);
            var meshTriangles = new NativeArray<int>(triangleCount, Allocator.TempJob);

            for (int i = 0; i < vertexCount; i++) {
                int uvIndex = i % crossSectionVertexCount;
                Vector2 uv = uvIndex < uvs.Length ? uvs[uvIndex] : Vector2.zero;

                meshVertices[i] = new VertexData {
                    Position = new float3(-vertices[i].x, vertices[i].y, vertices[i].z),
                    Normal = i < normals.Length ? new float3(-normals[i].x, normals[i].y, normals[i].z) : new float3(0, 1, 0),
                    UV = uv
                };
            }

            for (int i = 0; i < triangleCount; i += 3) {
                if (i + 2 < triangleCount) {
                    meshTriangles[i] = (int)indices[i + 2];
                    meshTriangles[i + 1] = (int)indices[i + 1];
                    meshTriangles[i + 2] = (int)indices[i];
                }
            }

            var meshData = new MeshData {
                Type = MeshType.Extrusion,
                SegmentId = segmentId,
                Vertices = meshVertices,
                Triangles = meshTriangles,
                VertexCount = vertexCount,
                TriangleCount = triangleCount
            };

            meshes.Add(meshData);
        }

        private static MeshData CombineSegmentMeshes(ref NativeList<MeshData> segmentMeshes, int segmentId) {
            if (segmentMeshes.Length == 1) {
                var singleMesh = segmentMeshes[0];
                segmentMeshes[0] = default;
                return new MeshData {
                    Type = MeshType.Segment,
                    SegmentId = segmentId,
                    Vertices = singleMesh.Vertices,
                    Triangles = singleMesh.Triangles,
                    VertexCount = singleMesh.VertexCount,
                    TriangleCount = singleMesh.TriangleCount
                };
            }

            int totalVertexCount = 0;
            int totalTriangleCount = 0;

            for (int i = 0; i < segmentMeshes.Length; i++) {
                var mesh = segmentMeshes[i];
                totalVertexCount += mesh.VertexCount;
                totalTriangleCount += mesh.TriangleCount;
            }

            var combinedVertices = new NativeArray<VertexData>(totalVertexCount, Allocator.TempJob);
            var combinedTriangles = new NativeArray<int>(totalTriangleCount, Allocator.TempJob);

            int vertexOffset = 0;
            int triangleOffset = 0;

            for (int i = 0; i < segmentMeshes.Length; i++) {
                var mesh = segmentMeshes[i];

                NativeArray<VertexData>.Copy(mesh.Vertices, 0, combinedVertices, vertexOffset, mesh.VertexCount);
                NativeArray<int>.Copy(mesh.Triangles, 0, combinedTriangles, triangleOffset, mesh.TriangleCount);

                for (int t = 0; t < mesh.TriangleCount; t++) {
                    combinedTriangles[triangleOffset + t] += vertexOffset;
                }

                vertexOffset += mesh.VertexCount;
                triangleOffset += mesh.TriangleCount;
            }

            return new MeshData {
                Type = MeshType.Segment,
                SegmentId = segmentId,
                Vertices = combinedVertices,
                Triangles = combinedTriangles,
                VertexCount = totalVertexCount,
                TriangleCount = totalTriangleCount
            };
        }

        private static void DisposeMeshData(ref NativeList<MeshData> meshes) {
            for (int i = 0; i < meshes.Length; i++) {
                var meshData = meshes[i];
                if (meshData.Vertices.IsCreated) meshData.Vertices.Dispose();
                if (meshData.Triangles.IsCreated) meshData.Triangles.Dispose();
            }
            meshes.Dispose();
        }

        [BurstCompile]
        public struct BuildJob : IJobParallelFor {
            [ReadOnly] public NativeArray<float3> Vertices;
            [ReadOnly] public NativeArray<float3> Normals;
            [ReadOnly] public NativeArray<float2> UVs;
            [ReadOnly] public float4x4 TransformMatrix;
            [WriteOnly] public NativeArray<VertexData> OutputData;

            public void Execute(int index) {
                var vertex = Vertices[index];
                var transformedVertex = math.transform(TransformMatrix, vertex);

                var normal = index < Normals.Length ? Normals[index] : float3.zero;
                var transformedNormal = index < Normals.Length ? math.normalize(math.mul(TransformMatrix, new float4(normal, 0f)).xyz) : float3.zero;

                var uv = index < UVs.Length ? UVs[index] : float2.zero;

                OutputData[index] = new VertexData {
                    Position = new float3(-transformedVertex.x, transformedVertex.y, transformedVertex.z),
                    Normal = new float3(-transformedNormal.x, transformedNormal.y, transformedNormal.z),
                    UV = uv
                };
            }
        }

        private static void CopyVertices(Vector3[] source, NativeArray<float3> destination) {
            for (int i = 0; i < source.Length; i++) {
                destination[i] = source[i];
            }
        }

        private static void CopyNormals(Vector3[] source, NativeArray<float3> destination) {
            for (int i = 0; i < source.Length; i++) {
                destination[i] = source[i];
            }
        }

        private static void CopyUVs(Vector2[] source, NativeArray<float2> destination) {
            for (int i = 0; i < source.Length; i++) {
                destination[i] = source[i];
            }
        }

        private static void CopyTriangles(int[] source, NativeArray<int> destination) {
            for (int i = 0; i < source.Length; i += 3) {
                if (i + 2 < source.Length) {
                    destination[i] = source[i + 2];
                    destination[i + 1] = source[i + 1];
                    destination[i + 2] = source[i];
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexData {
        public float3 Position;
        public float3 Normal;
        public float2 UV;
    }

    public enum MeshType {
        StartCap,
        EndCap,
        Duplication,
        Extrusion,
        Segment
    }

    public struct MeshData {
        public NativeArray<VertexData> Vertices;
        public NativeArray<int> Triangles;
        public MeshType Type;
        public int SegmentId;
        public int VertexCount;
        public int TriangleCount;
    }


    public static class ObjWriter {
        public static void WriteObjFile(string filePath, ref NativeList<MeshData> meshes) {
            var buffer = new StringBuilder(1024 * 1024); // 1MB buffer

            buffer.AppendLine("# Track meshes exported from KexEdit");
            buffer.AppendLine($"# Export date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            buffer.AppendLine("");

            int vertexOffset = 1;
            int normalOffset = 1;
            int uvOffset = 1;

            unsafe {
                for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++) {
                    var mesh = meshes[meshIndex];
                    string meshName = $"Segment_{mesh.SegmentId}";

                    buffer.AppendLine($"# Mesh: {meshName}");
                    buffer.AppendLine($"o {meshName}");

                    var vertexPtr = (VertexData*)mesh.Vertices.GetUnsafeReadOnlyPtr();

                    for (int i = 0; i < mesh.VertexCount; i++) {
                        var vertexData = vertexPtr[i];

                        buffer.AppendLine($"v {vertexData.Position.x:F6} {vertexData.Position.y:F6} {vertexData.Position.z:F6}");
                    }

                    bool hasNormals = false;
                    bool hasUVs = false;

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

            var utf8WithoutBom = new UTF8Encoding(false);
            File.WriteAllText(filePath, buffer.ToString(), utf8WithoutBom);
        }
    }
}
