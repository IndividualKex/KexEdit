using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Globalization;

namespace KexEdit.UI {
    public static class TrackStyleResourceLoader {
        public static TrackStyleConfig LoadConfig(string configPath) {
            string fullPath = Path.Combine(TrackStyleConfigManager.TrackStylesPath, configPath);

            TrackStyleConfig config;

            if (!File.Exists(fullPath)) {
                Debug.LogWarning($"Track style config not found at StreamingAssets/{configPath}. Using default configuration.");
                config = CreateDefaultConfig();
            }
            else {
                try {
                    string configText = File.ReadAllText(fullPath);
                    config = JsonUtility.FromJson<TrackStyleConfig>(configText);
                }
                catch (System.Exception e) {
                    Debug.LogError($"Failed to parse TrackStyleConfig: {e.Message}. Using default configuration.");
                    config = CreateDefaultConfig();
                }
            }

            config.SourceFileName = configPath;
            return config;
        }

        public static List<DuplicationMeshSettingsData> LoadDuplicationMeshes(
            List<DuplicationMeshConfig> configs,
            Material defaultMaterial,
            TrackStyleConfig trackConfig
        ) {
            var settings = new List<DuplicationMeshSettingsData>();

            foreach (var config in configs) {
                var mesh = LoadMesh(config.MeshPath);
                if (mesh == null) {
                    Debug.LogWarning($"Mesh not found at {config.MeshPath}. Skipping.");
                    continue;
                }

                var material = new Material(defaultMaterial);
                material.SetColor("_Color", config.GetColor(trackConfig));

                if (config.HasTexture()) {
                    var texture = LoadTexture(config.TexturePath);
                    if (texture != null) {
                        material.SetTexture("_MainTex", texture);
                    }
                    else {
                        Debug.LogWarning($"Texture not found at TrackStyles/{config.TexturePath}.");
                    }
                }

                int clampedOffset = config.Step > 0 ? config.Offset % config.Step : 0;
                if (clampedOffset < 0) clampedOffset += config.Step;

                settings.Add(new DuplicationMeshSettingsData {
                    Mesh = mesh,
                    Material = material,
                    Step = config.Step,
                    Offset = clampedOffset
                });
            }

            return settings;
        }

        public static List<ExtrusionMeshSettingsData> LoadExtrusionMeshes(
            List<ExtrusionMeshConfig> configs,
            Material defaultMaterial,
            TrackStyleConfig trackConfig
        ) {
            var settings = new List<ExtrusionMeshSettingsData>();

            foreach (var config in configs) {
                var mesh = LoadMesh(config.MeshPath);
                if (mesh == null) {
                    Debug.LogWarning($"Mesh not found at {config.MeshPath}. Skipping.");
                    continue;
                }

                if (ExtrusionMeshConverter.Convert(mesh, out var outputMesh)) {
                    mesh = outputMesh;
                }
                else {
                    var fallbackMesh = LoadMesh("FallbackRail.obj");
                    if (fallbackMesh != null && ExtrusionMeshConverter.Convert(fallbackMesh, out fallbackMesh)) {
                        Debug.LogWarning($"Mesh conversion failed for {config.MeshPath}, using fallback mesh.");
                        mesh = fallbackMesh;
                    }
                }

                var material = new Material(defaultMaterial);
                material.SetColor("_Color", config.GetColor(trackConfig));

                if (config.HasTexture()) {
                    var texture = LoadTexture(config.TexturePath);
                    if (texture != null) {
                        material.SetTexture("_MainTex", texture);
                    }
                    else {
                        Debug.LogWarning($"Texture not found at TrackStyles/{config.TexturePath}.");
                    }
                }

                settings.Add(new ExtrusionMeshSettingsData {
                    Mesh = mesh,
                    Material = material
                });
            }

            return settings;
        }

        public static List<CapMeshSettingsData> LoadCapMeshes(
            List<CapMeshConfig> configs,
            Material defaultMaterial,
            TrackStyleConfig trackConfig
        ) {
            var settings = new List<CapMeshSettingsData>();

            foreach (var config in configs) {
                var mesh = LoadMesh(config.MeshPath);
                if (mesh == null) {
                    Debug.LogWarning($"Cap mesh not found at {config.MeshPath}. Skipping.");
                    continue;
                }

                var material = new Material(defaultMaterial);
                material.SetColor("_Color", config.GetColor(trackConfig));

                if (config.HasTexture()) {
                    var texture = LoadTexture(config.TexturePath);
                    if (texture != null) {
                        material.SetTexture("_MainTex", texture);
                    }
                    else {
                        Debug.LogWarning($"Texture not found at TrackStyles/{config.TexturePath}.");
                    }
                }

                settings.Add(new CapMeshSettingsData {
                    Mesh = mesh,
                    Material = material
                });
            }

            return settings;
        }

        private static Mesh LoadMesh(string path) {
            string fullPath = Path.Combine(TrackStyleConfigManager.TrackStylesPath, path);
            if (path.EndsWith(".obj")) {
                try {
                    var mesh = ParseTriangulatedObj(fullPath);
                    if (mesh != null) {
                        return mesh;
                    }
                }
                catch (System.Exception e) {
                    Debug.LogError($"Failed to load mesh {path}: {e.Message}");
                }

                if (path != "FallbackRail.obj") {
                    Debug.LogWarning($"Mesh {path} failed to load, attempting fallback to FallbackRail.obj");
                    var fallbackPath = Path.Combine(TrackStyleConfigManager.TrackStylesPath, "FallbackRail.obj");
                    try {
                        var fallbackMesh = ParseTriangulatedObj(fallbackPath);
                        if (fallbackMesh != null) {
                            return fallbackMesh;
                        }
                    }
                    catch (System.Exception e) {
                        Debug.LogError($"Fallback mesh FallbackRail.obj also failed to load: {e.Message}");
                    }
                }
            }
            else {
                Debug.LogError($"Unsupported file type: {path}");
            }
            return null;
        }

        private static Texture2D LoadTexture(string path) {
            string fullPath = Path.Combine(TrackStyleConfigManager.TrackStylesPath, path);

            if (!File.Exists(fullPath)) {
                Debug.LogError($"Texture file does not exist: {fullPath}");
                return null;
            }

            try {
                byte[] data = File.ReadAllBytes(fullPath);

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (texture.LoadImage(data)) {
                    texture.Apply();
                    return texture;
                }
                else {
                    Debug.LogError($"Failed to load texture {path}");
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            catch (System.Exception e) {
                Debug.LogError($"Failed to load texture {path}: {e.Message}");
            }

            return null;
        }

        private static TrackStyleConfig CreateDefaultConfig() {
            var config = new TrackStyleConfig {
                Colors = new Color[] { new(0.5f, 0.5f, 0.5f, 1), new(0.6f, 0.435f, 0.27f, 1) },
                Styles = new List<TrackStyleMeshConfig> {
                    new() {
                        Spacing = 0.4f,
                        Threshold = 0f,
                        DuplicationMeshes = new List<DuplicationMeshConfig> {
                            new() {
                                MeshPath = "ClassicTie.obj",
                                Step = 2,
                                Offset = 0,
                                ColorIndex = 1
                            }
                        },
                        ExtrusionMeshes = new List<ExtrusionMeshConfig> {
                            new() {
                                MeshPath = "ClassicRail.obj",
                                ColorIndex = 1
                            },
                            new() {
                                MeshPath = "ClassicRail_Topper.obj"
                            }
                        },
                        StartCapMeshes = new List<CapMeshConfig> {
                            new() {
                                MeshPath = "ClassicRail_StartCap.obj",
                                ColorIndex = 1
                            },
                            new() {
                                MeshPath = "ClassicRail_Topper_StartCap.obj"
                            }
                        },
                        EndCapMeshes = new List<CapMeshConfig> {
                            new() {
                                MeshPath = "ClassicRail_EndCap.obj",
                                ColorIndex = 1
                            },
                            new() {
                                MeshPath = "ClassicRail_Topper_EndCap.obj"
                            }
                        }
                    }
                },
                SourceFileName = "Classic.json"
            };
            return config;
        }

        private static Mesh ParseTriangulatedObj(string path) {
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
