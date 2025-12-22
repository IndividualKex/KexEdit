using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KexEdit.Legacy {
    public static class ObjImporter {
        public static Mesh LoadMesh(string filePath) {
            if (!File.Exists(filePath)) {
                Debug.LogError($"OBJ file not found: {filePath}");
                return null;
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            var meshVertices = new List<Vector3>();
            var meshNormals = new List<Vector3>();
            var meshUVs = new List<Vector2>();

            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines) {
                string[] parts = line.Split(' ');
                if (parts.Length == 0) continue;

                switch (parts[0]) {
                    case "v":
                        if (parts.Length >= 4) {
                            vertices.Add(new Vector3(
                                float.Parse(parts[1]),
                                float.Parse(parts[2]),
                                float.Parse(parts[3])
                            ));
                        }
                        break;

                    case "vn":
                        if (parts.Length >= 4) {
                            normals.Add(new Vector3(
                                float.Parse(parts[1]),
                                float.Parse(parts[2]),
                                float.Parse(parts[3])
                            ));
                        }
                        break;

                    case "vt":
                        if (parts.Length >= 3) {
                            uvs.Add(new Vector2(
                                float.Parse(parts[1]),
                                float.Parse(parts[2])
                            ));
                        }
                        break;

                    case "f":
                        if (parts.Length >= 4) {
                            ParseFace(parts, vertices, normals, uvs,
                                meshVertices, meshNormals, meshUVs, triangles);
                        }
                        break;
                }
            }

            var mesh = new Mesh {
                name = Path.GetFileNameWithoutExtension(filePath),
                vertices = meshVertices.ToArray(),
                normals = meshNormals.Count == meshVertices.Count ? meshNormals.ToArray() : null,
                uv = meshUVs.Count == meshVertices.Count ? meshUVs.ToArray() : null,
                triangles = triangles.ToArray()
            };

            if (meshNormals.Count == 0) {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();

            return mesh;
        }

        private static void ParseFace(string[] parts,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<Vector3> meshVertices, List<Vector3> meshNormals, List<Vector2> meshUVs,
            List<int> triangles) {

            int[] faceIndices = new int[parts.Length - 1];

            for (int i = 1; i < parts.Length; i++) {
                string[] indices = parts[i].Split('/');

                int vertexIndex = int.Parse(indices[0]) - 1;
                int uvIndex = indices.Length > 1 && !string.IsNullOrEmpty(indices[1])
                    ? int.Parse(indices[1]) - 1 : -1;
                int normalIndex = indices.Length > 2 && !string.IsNullOrEmpty(indices[2])
                    ? int.Parse(indices[2]) - 1 : -1;

                meshVertices.Add(vertices[vertexIndex]);

                if (uvIndex >= 0 && uvIndex < uvs.Count) {
                    meshUVs.Add(uvs[uvIndex]);
                }
                else {
                    meshUVs.Add(Vector2.zero);
                }

                if (normalIndex >= 0 && normalIndex < normals.Count) {
                    meshNormals.Add(normals[normalIndex]);
                }
                else {
                    meshNormals.Add(Vector3.up);
                }

                faceIndices[i - 1] = meshVertices.Count - 1;
            }

            for (int i = 1; i < faceIndices.Length - 1; i++) {
                triangles.Add(faceIndices[0]);
                triangles.Add(faceIndices[i]);
                triangles.Add(faceIndices[i + 1]);
            }
        }
    }
}
