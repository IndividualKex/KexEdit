using UnityEngine;
using System.Collections.Generic;
using System.IO;
using KexEdit.UI;

namespace KexEdit {
    public static class TrackStyleResourceLoader {
        private static string TrackMeshPath => Path.Combine(Application.streamingAssetsPath, "TrackStyles");

        public static TrackStyleConfig LoadConfig(string configPath) {
            string fullPath = Path.Combine(TrackMeshPath, configPath);

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
                    Debug.LogError($"Failed to parse TrackMeshConfig: {e.Message}. Using default configuration.");
                    config = CreateDefaultConfig();
                }
            }

            config.SourceFileName = configPath;
            return config;
        }

        public static List<DuplicationMeshSettings> LoadDuplicationMeshes(
            List<DuplicationMeshConfig> configs,
            Material defaultMaterial,
            TrackStyleConfig trackConfig
        ) {
            var settings = new List<DuplicationMeshSettings>();

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

                settings.Add(new DuplicationMeshSettings {
                    Mesh = mesh,
                    Material = material,
                    Step = config.Step,
                    Offset = clampedOffset
                });
            }

            return settings;
        }

        public static List<ExtrusionMeshSettings> LoadExtrusionMeshes(
            List<ExtrusionMeshConfig> configs,
            Material defaultMaterial,
            TrackStyleConfig trackConfig
        ) {
            var settings = new List<ExtrusionMeshSettings>();

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

                settings.Add(new ExtrusionMeshSettings {
                    Mesh = mesh,
                    Material = material
                });
            }

            return settings;
        }

        public static List<CapMeshSettings> LoadCapMeshes(
            List<CapMeshConfig> configs,
            Material defaultMaterial,
            TrackStyleConfig trackConfig
        ) {
            var settings = new List<CapMeshSettings>();

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

                settings.Add(new CapMeshSettings {
                    Mesh = mesh,
                    Material = material
                });
            }

            return settings;
        }

        private static Mesh LoadMesh(string path) {
            string fullPath = Path.Combine(TrackMeshPath, path);
            if (path.EndsWith(".obj")) {
                try {
                    var mesh = ImportManager.ParseObjFile(fullPath);
                    if (mesh != null) {
                        return mesh;
                    }
                }
                catch (System.Exception e) {
                    Debug.LogError($"Failed to load mesh {path}: {e.Message}");
                }

                if (path != "FallbackRail.obj") {
                    Debug.LogWarning($"Mesh {path} failed to load, attempting fallback to FallbackRail.obj");
                    var fallbackPath = Path.Combine(TrackMeshPath, "FallbackRail.obj");
                    try {
                        var fallbackMesh = ImportManager.ParseObjFile(fallbackPath);
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
            string fullPath = Path.Combine(TrackMeshPath, path);

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
                    Object.DestroyImmediate(texture);
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
                                MeshPath = "DefaultTie.obj",
                                Step = 2,
                                Offset = 0,
                                ColorIndex = 1
                            }
                        },
                        ExtrusionMeshes = new List<ExtrusionMeshConfig> {
                            new() {
                                MeshPath = "DefaultRail.obj",
                                ColorIndex = 1
                            },
                            new() {
                                MeshPath = "DefaultRail_Topper.obj"
                            }
                        },
                        StartCapMeshes = new List<CapMeshConfig> {
                            new() {
                                MeshPath = "DefaultRail_StartCap.obj",
                                ColorIndex = 1
                            },
                            new() {
                                MeshPath = "DefaultRail_Topper_StartCap.obj"
                            }
                        },
                        EndCapMeshes = new List<CapMeshConfig> {
                            new() {
                                MeshPath = "DefaultRail_EndCap.obj",
                                ColorIndex = 1
                            },
                            new() {
                                MeshPath = "DefaultRail_Topper_EndCap.obj"
                            }
                        }
                    }
                }
            };
            config.SourceFileName = "Default.json";
            return config;
        }
    }
}
