using UnityEngine;
using System.Collections.Generic;

namespace KexEdit.Legacy {
    public static class ExtrusionMeshConverter {
        public static bool Convert(Mesh inputMesh, out Mesh outputMesh) {
            outputMesh = null;

            var inVertices = inputMesh.vertices;
            var inUVs = inputMesh.uv;
            var inNormals = inputMesh.normals;
            var inTriangles = inputMesh.triangles;

            var vertices = new List<VertexData>();
            for (int i = 0; i < inVertices.Length; i++) {
                vertices.Add(new VertexData {
                    Position = inVertices[i],
                    UV = inUVs[i],
                    Normal = inNormals.Length > i ? inNormals[i] : Vector3.up
                });
            }

            // Validate that the mesh has exactly 2 cross sections
            var layers = new Dictionary<int, HashSet<VertexData>>();
            for (int i = 0; i < vertices.Count; i++) {
                var vertex = vertices[i];
                int z = Mathf.RoundToInt(vertex.Position.z * 1000f);
                if (!layers.ContainsKey(z)) {
                    layers.Add(z, new HashSet<VertexData>());
                }
                layers[z].Add(vertex);
            }
            if (layers.Count != 2) {
                UnityEngine.Debug.LogError("Mesh does not have exactly 2 cross sections");
                return false;
            }

            // Validate that the layers have the same number of vertices
            HashSet<VertexData> layer0 = null;
            HashSet<VertexData> layer1 = null;
            foreach (var layer in layers) {
                if (layer0 == null) {
                    layer0 = layer.Value;
                }
                else {
                    layer1 = layer.Value;
                }
            }
            if (layer0.Count != layer1.Count) {
                UnityEngine.Debug.LogError("Cross sections have different counts");
                return false;
            }

            // Detect edges and build adjacency in one pass
            var edges = new List<Edge>();
            var adjacency = new Dictionary<VertexData, List<VertexData>>();

            void AddEdge(VertexData a, VertexData b) {
                edges.Add(new Edge(a, b));
                if (!adjacency.ContainsKey(a)) {
                    adjacency[a] = new List<VertexData>();
                }
                if (!adjacency.ContainsKey(b)) {
                    adjacency[b] = new List<VertexData>();
                }
                adjacency[a].Add(b);
                adjacency[b].Add(a);
            }

            for (int i = 0; i < inTriangles.Length; i += 3) {
                int ai = inTriangles[i];
                int bi = inTriangles[i + 1];
                int ci = inTriangles[i + 2];

                VertexData a = vertices[ai];
                VertexData b = vertices[bi];
                VertexData c = vertices[ci];

                if (layer0.Contains(a) && layer0.Contains(b)) {
                    AddEdge(a, b);
                }
                if (layer0.Contains(b) && layer0.Contains(c)) {
                    AddEdge(b, c);
                }
                if (layer0.Contains(a) && layer0.Contains(c)) {
                    AddEdge(a, c);
                }
            }

            // Validate no intersections
            foreach (var vertex in adjacency.Keys) {
                if (adjacency[vertex].Count > 2) {
                    Debug.LogError("Cross section contains vertex intersections");
                    return false;
                }
            }

            // Validate cross section alignment
            var layer1Projection = new HashSet<Vector2>();
            foreach (var vertex in layer1) {
                layer1Projection.Add(vertex.Position);
            }
            foreach (var edge in edges) {
                Vector2 a = edge.A.Position;
                Vector2 b = edge.B.Position;

                if (!layer1Projection.Contains(a) || !layer1Projection.Contains(b)) {
                    UnityEngine.Debug.LogError("Cross sections are not aligned");
                    return false;
                }
            }

            // Detect edge loops
            var edgeLoops = new List<List<Edge>>();
            var visited = new HashSet<VertexData>();
            foreach (var start in adjacency.Keys) {
                if (visited.Contains(start)) continue;

                var loop = new List<Edge>();
                var current = start;

                while (true) {
                    visited.Add(current);

                    VertexData? next = null;
                    foreach (var neighbor in adjacency[current]) {
                        if (!visited.Contains(neighbor)) {
                            next = neighbor;
                            break;
                        }
                    }

                    if (!next.HasValue && loop.Count > 0) {
                        foreach (var neighbor in adjacency[current]) {
                            if (neighbor.Equals(start)) {
                                next = neighbor;
                                break;
                            }
                        }
                    }

                    if (!next.HasValue) break;

                    loop.Add(new Edge(current, next.Value));
                    if (next.Value.Equals(start)) break;
                    current = next.Value;
                }

                if (loop.Count > 0) edgeLoops.Add(loop);
            }

            for (int i = 0; i < edgeLoops.Count; i++) {
                var loop = edgeLoops[i];
                if (loop.Count < 3) continue;

                float signedArea = 0f;
                for (int j = 0; j < loop.Count; j++) {
                    var current = loop[j].A.Position;
                    var next = loop[j].B.Position;
                    signedArea += (next.x - current.x) * (next.y + current.y);
                }

                if (signedArea < 0) {
                    var reversedLoop = new List<Edge>();
                    for (int j = loop.Count - 1; j >= 0; j--) {
                        var edge = loop[j];
                        reversedLoop.Add(new Edge(edge.B, edge.A));
                    }
                    edgeLoops[i] = reversedLoop;
                }
            }

            // Compute extrusion
            int edgeCount = 0;
            foreach (var loop in edgeLoops) {
                edgeCount += loop.Count;
            }
            int vertexCount = edgeCount * 4;
            int indexCount = edgeCount * 6;

            Vector3[] outVertices = new Vector3[vertexCount];
            Vector2[] outUVs = new Vector2[vertexCount];
            Vector3[] outNormals = new Vector3[vertexCount];
            int[] outTriangles = new int[indexCount];

            int index = 0;
            foreach (var loop in edgeLoops) {
                for (int i = 0; i < loop.Count; i++) {
                    var edge = loop[i];

                    Vector3 a = edge.A.Position;
                    Vector3 b = edge.B.Position;
                    a.z = 0f;
                    b.z = 0f;
                    Vector3 c = a + Vector3.forward;
                    Vector3 d = b + Vector3.forward;

                    int ai = index * 2;
                    int bi = ai + 1;
                    int ci = ai + edgeCount * 2;
                    int di = bi + edgeCount * 2;

                    outVertices[ai] = a;
                    outVertices[bi] = b;
                    outVertices[ci] = c;
                    outVertices[di] = d;

                    outUVs[ai] = edge.A.UV;
                    outUVs[bi] = edge.B.UV;
                    outUVs[ci] = edge.A.UV;
                    outUVs[di] = edge.B.UV;

                    outNormals[ai] = edge.A.Normal;
                    outNormals[bi] = edge.B.Normal;
                    outNormals[ci] = edge.A.Normal;
                    outNormals[di] = edge.B.Normal;

                    outTriangles[index * 6] = ai;
                    outTriangles[index * 6 + 1] = ci;
                    outTriangles[index * 6 + 2] = di;
                    outTriangles[index * 6 + 3] = ai;
                    outTriangles[index * 6 + 4] = di;
                    outTriangles[index * 6 + 5] = bi;

                    index++;
                }
            }

            outputMesh = new Mesh {
                name = inputMesh.name
            };
            outputMesh.SetVertices(outVertices);
            outputMesh.SetUVs(0, outUVs);
            outputMesh.SetNormals(outNormals);
            outputMesh.SetIndices(outTriangles, MeshTopology.Triangles, 0);

            return true;
        }

        private struct VertexData {
            public Vector3 Position;
            public Vector2 UV;
            public Vector3 Normal;
        }

        private struct Edge {
            public VertexData A;
            public VertexData B;

            public Edge(VertexData a, VertexData b) {
                A = a;
                B = b;
            }
        }
    }
}
