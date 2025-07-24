using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using GLTFast;
using SFB;

namespace KexEdit.UI {
    public static class ImportManager {
        public static void ShowImportDialog(VisualElement root, Action<string> onSuccess = null) {
            var extensions = new ExtensionFilter[] {
                new("glTF", "glb", "gltf"),
                new("OBJ", "obj"),
                new("All Files", "*")
            };

            string path = FileManager.ShowOpenFileDialog(extensions);

            if (string.IsNullOrEmpty(path)) {
                Debug.Log("Import canceled or no file selected.");
                return;
            }

            onSuccess?.Invoke(path);
        }

        public static async void ImportGltfFileAsync(string path, Action<GameObject> onSuccess = null) {
            var gltf = new GltfImport();
            bool success = await gltf.Load(path);

            if (!success) {
                Debug.LogError("Failed to load glTF.");
                return;
            }

            var rootGO = new GameObject($"Imported glTF: {Path.GetFileNameWithoutExtension(path)}");

            success = await gltf.InstantiateMainSceneAsync(rootGO.transform);

            if (!success) {
                Debug.LogError("Failed to instantiate glTF.");
                GameObject.Destroy(rootGO);
            }
            else {
                onSuccess?.Invoke(rootGO);
            }
        }

        public static void ImportObjFile(string path, Action<GameObject> onSuccess = null) {
            try {
                Mesh mesh = ParseObjFile(path);

                if (mesh == null) {
                    Debug.LogError("Failed to parse OBJ file.");
                    return;
                }

                var rootGO = new GameObject($"Imported OBJ: {Path.GetFileNameWithoutExtension(path)}");
                var meshFilter = rootGO.AddComponent<MeshFilter>();
                var meshRenderer = rootGO.AddComponent<MeshRenderer>();

                meshFilter.mesh = mesh;

                meshRenderer.material = Resources.Load<Material>("Default-PBR");

                onSuccess?.Invoke(rootGO);
            }
            catch (Exception e) {
                Debug.LogError($"Error importing OBJ file: {e.Message}");
            }
        }

        public static Mesh ParseObjFile(string path) {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            var finalVertices = new List<Vector3>();
            var finalNormals = new List<Vector3>();
            var finalUvs = new List<Vector2>();

            var vertexDict = new Dictionary<string, int>();

            string[] lines = File.ReadAllLines(path);

            foreach (string line in lines) {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0) continue;

                switch (parts[0]) {
                    case "v":
                        if (parts.Length != 4) {
                            Debug.LogError($"Invalid vertex line: {line}, expected 3 values.");
                            continue;
                        }

                        float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float z = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        vertices.Add(new Vector3(x, y, z));
                        break;

                    case "vn":
                        if (parts.Length != 4) {
                            Debug.LogError($"Invalid normal line: {line}, expected 3 values.");
                            continue;
                        }

                        float xn = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float yn = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float zn = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        normals.Add(new Vector3(xn, yn, zn));
                        break;

                    case "vt":
                        if (parts.Length != 3) {
                            Debug.LogError($"Invalid uv line: {line}, expected 2 values.");
                            continue;
                        }

                        float u = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float v = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        uvs.Add(new Vector2(u, v));
                        break;

                    case "f":
                        if (parts.Length != 4) {
                            Debug.LogError($"Invalid face line: {line}. Only triangles are supported.");
                            continue;
                        }

                        for (int i = 1; i < parts.Length; i++) {
                            string vertexKey = parts[i];

                            if (!vertexDict.ContainsKey(vertexKey)) {
                                string[] indices = vertexKey.Split('/');

                                int vertexIndex = int.Parse(indices[0]) - 1;
                                int uvIndex = indices.Length > 1 && !string.IsNullOrEmpty(indices[1]) ? int.Parse(indices[1]) - 1 : -1;
                                int normalIndex = indices.Length > 2 && !string.IsNullOrEmpty(indices[2]) ? int.Parse(indices[2]) - 1 : -1;

                                finalVertices.Add(vertices[vertexIndex]);

                                if (uvIndex >= 0 && uvIndex < uvs.Count) {
                                    finalUvs.Add(uvs[uvIndex]);

                                }
                                else {
                                    finalUvs.Add(Vector2.zero);
                                }

                                if (normalIndex >= 0 && normalIndex < normals.Count) {
                                    finalNormals.Add(normals[normalIndex]);
                                }
                                else {
                                    finalNormals.Add(Vector3.up);
                                }

                                vertexDict[vertexKey] = finalVertices.Count - 1;
                            }

                            triangles.Add(vertexDict[vertexKey]);
                        }
                        break;
                }
            }

            if (finalVertices.Count == 0) {
                Debug.LogError("No vertices found in OBJ file.");
                return null;
            }

            var mesh = new Mesh {
                vertices = finalVertices.ToArray(),
                triangles = triangles.ToArray()
            };

            if (finalUvs.Count > 0) {
                mesh.uv = finalUvs.ToArray();
            }

            if (finalNormals.Count > 0) {
                mesh.normals = finalNormals.ToArray();
            }
            else {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            mesh.name = Path.GetFileNameWithoutExtension(path);

            return mesh;
        }
    }
}
