using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace KexEdit.Editor {
    public class KexEditEditorUtils {
        [MenuItem("KexEdit/Generate Default Rail")]
        public static void GenerateDefaultRail() {
            var leftRailVertices = new NativeArray<Vector3>(12, Allocator.Temp) {
                [0] = new Vector3(-.656f, 0f, 0f),
                [1] = new Vector3(-.656f, .266f, 0f),
                [2] = new Vector3(-.6005f, .266f, 0f),
                [3] = new Vector3(-.6005f, .342f, 0f),
                [4] = new Vector3(-.59825f, .342f, 0f),
                [5] = new Vector3(-.59825f, .353f, 0f),
                [6] = new Vector3(-.48825f, .353f, 0f),
                [7] = new Vector3(-.48825f, .342f, 0f),
                [8] = new Vector3(-.4005f, .342f, 0f),
                [9] = new Vector3(-.4005f, .266f, 0f),
                [10] = new Vector3(-.456f, .266f, 0f),
                [11] = new Vector3(-.456f, 0f, 0f),
            };
            var leftRailUVs = new NativeArray<Vector2>(12, Allocator.Temp);
            for (int i = 0; i < 12; i++) {
                if (i >= 4 && i < 7) {
                    leftRailUVs[i] = new Vector2(0.25f, 0.5f);
                }
                else {
                    leftRailUVs[i] = new Vector2(0.75f, 0.5f);
                }
            }

            /* var rightRailVertices = new NativeArray<Vector3>(12, Allocator.Temp);
            var rightRailUVs = new NativeArray<Vector2>(12, Allocator.Temp);
            for (int i = 0; i < leftRailVertices.Length; i++) {
                rightRailVertices[leftRailVertices.Length - i - 1] = new Vector3(
                    -leftRailVertices[i].x, leftRailVertices[i].y, leftRailVertices[i].z
                );
                rightRailUVs[leftRailVertices.Length - i - 1] = leftRailUVs[i];
            } */

            var edges = new NativeList<Edge>(Allocator.Temp);
            for (int i = 0; i < leftRailVertices.Length; i++) {
                edges.Add(new Edge {
                    A = leftRailVertices[i],
                    B = leftRailVertices[(i + 1) % leftRailVertices.Length],
                    UV = leftRailUVs[i]
                });
            }
            /* for (int i = 0; i < rightRailVertices.Length; i++) {
                edges.Add(new Edge {
                    A = rightRailVertices[i],
                    B = rightRailVertices[(i + 1) % rightRailVertices.Length],
                    UV = rightRailUVs[i]
                });
            } */
            leftRailVertices.Dispose();
            // rightRailVertices.Dispose();

            int edgeCount = edges.Length;
            int vertexCount = edgeCount * 4;
            int indexCount = edgeCount * 6;

            NativeArray<Vector3> vertices = new(vertexCount, Allocator.Temp);
            NativeArray<Vector2> uvs = new(vertexCount, Allocator.Temp);
            NativeArray<Vector3> normals = new(vertexCount, Allocator.Temp);
            NativeArray<uint> indices = new(indexCount, Allocator.Temp);

            for (int i = 0; i < edgeCount; i++) {
                Vector3 a = edges[i].A;
                Vector3 b = edges[i].B;
                Vector3 c = a + Vector3.forward;
                Vector3 d = b + Vector3.forward;

                Vector3 normal = Vector3.Cross(b - a, Vector3.back).normalized;

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
                name = "Default Rail"
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);

            string path = "Assets/Resources/FallbackRail.asset";
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Default rail generated at {path}");
        }

        struct Edge {
            public Vector3 A;
            public Vector3 B;
            public Vector2 UV;
        }
    }
}
