using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace KexEdit.Editor {
    public class KexEditEditorUtils {
        [MenuItem("KexEdit/Generate Rail Cross Section")]
        public static void GenerateRailCrossSection() {
            var leftRailVertices = new NativeArray<float3>(12, Allocator.Temp) {
                [0] = new float3(-.656f, 0f, 0f),
                [1] = new float3(-.656f, .266f, 0f),
                [2] = new float3(-.6005f, .266f, 0f),
                [3] = new float3(-.6005f, .342f, 0f),
                [4] = new float3(-.59825f, .342f, 0f),
                [5] = new float3(-.59825f, .353f, 0f),
                [6] = new float3(-.48825f, .353f, 0f),
                [7] = new float3(-.48825f, .342f, 0f),
                [8] = new float3(-.4005f, .342f, 0f),
                [9] = new float3(-.4005f, .266f, 0f),
                [10] = new float3(-.456f, .266f, 0f),
                [11] = new float3(-.456f, 0f, 0f),
            };
            var leftRailUVs = new NativeArray<float2>(12, Allocator.Temp);
            for (int i = 0; i < 12; i++) {
                if (i >= 4 && i < 7) {
                    leftRailUVs[i] = new float2(0.25f, 0.5f);
                }
                else {
                    leftRailUVs[i] = new float2(0.75f, 0.5f);
                }
            }

            var rightRailVertices = new NativeArray<float3>(12, Allocator.Temp);
            var rightRailUVs = new NativeArray<float2>(12, Allocator.Temp);
            for (int i = 0; i < leftRailVertices.Length; i++) {
                rightRailVertices[leftRailVertices.Length - i - 1] = leftRailVertices[i] * new float3(-1f, 1f, 1f);
                rightRailUVs[leftRailVertices.Length - i - 1] = leftRailUVs[i];
            }

            var edges = new NativeList<Edge>(Allocator.Temp);
            for (int i = 0; i < leftRailVertices.Length; i++) {
                edges.Add(new Edge {
                    A = leftRailVertices[i],
                    B = leftRailVertices[(i + 1) % leftRailVertices.Length],
                    UV = leftRailUVs[i]
                });
            }
            for (int i = 0; i < rightRailVertices.Length; i++) {
                edges.Add(new Edge {
                    A = rightRailVertices[i],
                    B = rightRailVertices[(i + 1) % rightRailVertices.Length],
                    UV = rightRailUVs[i]
                });
            }
            leftRailVertices.Dispose();
            rightRailVertices.Dispose();

            int edgeCount = edges.Length;
            int vertexCount = edgeCount * 4;
            int indexCount = edgeCount * 6;

            NativeArray<float3> vertices = new(vertexCount, Allocator.Temp);
            NativeArray<float2> uvs = new(vertexCount, Allocator.Temp);
            NativeArray<float3> normals = new(vertexCount, Allocator.Temp);
            NativeArray<uint> indices = new(indexCount, Allocator.Temp);

            for (int i = 0; i < edgeCount; i++) {
                float3 a = edges[i].A;
                float3 b = edges[i].B;
                float3 c = a + math.forward();
                float3 d = b + math.forward();

                float3 normal = math.normalize(math.cross(b - a, math.back()));

                int ai = i * 2;
                int bi = ai + 1;
                int ci = ai + edgeCount * 2;
                int di = bi + edgeCount * 2;

                vertices[ai] = a;
                vertices[bi] = b;
                vertices[ci] = c;
                vertices[di] = d;

                uvs[ai] = edges[i].UV;
                uvs[bi] = edges[i].UV;
                uvs[ci] = edges[i].UV;
                uvs[di] = edges[i].UV;

                normals[ai] = normal;
                normals[bi] = normal;
                normals[ci] = normal;
                normals[di] = normal;

                indices[i * 6] = (uint)ai;
                indices[i * 6 + 1] = (uint)ci;
                indices[i * 6 + 2] = (uint)di;
                indices[i * 6 + 3] = (uint)ai;
                indices[i * 6 + 4] = (uint)di;
                indices[i * 6 + 5] = (uint)bi;
            }

            Mesh mesh = new() {
                name = "Rail Cross Section"
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);

            string path = "Assets/Meshes/RailCrossSection.asset";
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Rail cross section generated at {path}");
        }

        struct Edge {
            public float3 A;
            public float3 B;
            public float2 UV;
        }
    }
}
