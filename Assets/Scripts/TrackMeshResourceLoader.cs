using UnityEngine;
using System.Collections.Generic;
using System.IO;
using KexEdit.UI;

namespace KexEdit {
    public static class TrackMeshResourceLoader {
        public static TrackMeshConfigData LoadConfig(string configPath = "Config.json") {
            string fullPath = Path.Combine(Application.streamingAssetsPath, configPath);

            if (!File.Exists(fullPath)) {
                Debug.LogWarning($"TrackMeshConfig not found at StreamingAssets/{configPath}. Using default configuration.");
                return CreateDefaultConfig();
            }

            try {
                string configText = File.ReadAllText(fullPath);
                return JsonUtility.FromJson<TrackMeshConfigData>(configText);
            }
            catch (System.Exception e) {
                Debug.LogError($"Failed to parse TrackMeshConfig: {e.Message}. Using default configuration.");
                return CreateDefaultConfig();
            }
        }

        public static List<DuplicationMeshSettings> LoadDuplicationMeshes(
            List<DuplicationMeshConfigData> configs,
            Material defaultMaterial
        ) {
            var settings = new List<DuplicationMeshSettings>();

            foreach (var config in configs) {
                var mesh = LoadMesh(config.MeshPath);
                if (mesh == null) {
                    Debug.LogWarning($"Mesh not found at {config.MeshPath}. Skipping.");
                    continue;
                }

                var material = new Material(defaultMaterial);
                material.SetColor("_Color", config.Color);

                if (!string.IsNullOrEmpty(config.TexturePath)) {
                    var texture = LoadTexture(config.TexturePath);
                    if (texture != null) {
                        material.SetTexture("_MainTex", texture);
                    }
                    else {
                        Debug.LogWarning($"Texture not found at StreamingAssets/{config.TexturePath}.");
                    }
                }

                settings.Add(new DuplicationMeshSettings {
                    Mesh = mesh,
                    Material = material,
                    Step = config.Step
                });
            }

            return settings;
        }

        public static List<ExtrusionMeshSettings> LoadExtrusionMeshes(
            List<ExtrusionMeshConfigData> configs,
            Material defaultMaterial
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
                    if (fallbackMesh != null) {
                        Debug.LogWarning($"Mesh conversion failed for {config.MeshPath}, using fallback mesh.");
                        mesh = fallbackMesh;
                    }
                }

                var material = new Material(defaultMaterial);
                material.SetColor("_Color", config.Color);

                if (!string.IsNullOrEmpty(config.TexturePath)) {
                    var texture = LoadTexture(config.TexturePath);
                    if (texture != null) {
                        material.SetTexture("_MainTex", texture);
                    }
                    else {
                        Debug.LogWarning($"Texture not found at StreamingAssets/{config.TexturePath}.");
                    }
                }

                settings.Add(new ExtrusionMeshSettings {
                    Mesh = mesh,
                    Material = material
                });
            }

            return settings;
        }

        public static List<DuplicationGizmoSettings> LoadDuplicationGizmos(
            List<DuplicationGizmoConfigData> configs,
            Material defaultMaterial
        ) {
            var settings = new List<DuplicationGizmoSettings>();

            foreach (var config in configs) {
                var material = new Material(defaultMaterial);
                material.SetColor("_Color", config.Color);

                settings.Add(new DuplicationGizmoSettings {
                    Material = material,
                    StartHeart = config.StartHeart,
                    EndHeart = config.EndHeart
                });
            }

            return settings;
        }

        public static List<ExtrusionGizmoSettings> LoadExtrusionGizmos(
            List<ExtrusionGizmoConfigData> configs,
            Material defaultMaterial
        ) {
            var settings = new List<ExtrusionGizmoSettings>();

            foreach (var config in configs) {
                var material = new Material(defaultMaterial);
                material.SetColor("_Color", config.Color);

                settings.Add(new ExtrusionGizmoSettings {
                    Material = material,
                    Heart = config.Heart
                });
            }

            return settings;
        }

        private static Mesh LoadMesh(string path) {
            string fullPath = Path.Combine(Application.streamingAssetsPath, path);
            if (path.EndsWith(".obj")) {
                return ImportManager.ParseObjFile(fullPath);
            }
            Debug.LogError($"Unsupported file type: {path}");
            return null;
        }

        private static Texture2D LoadTexture(string path) {
            string fullPath = Path.Combine(Application.streamingAssetsPath, path);

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

        private static TrackMeshConfigData CreateDefaultConfig() {
            return new TrackMeshConfigData {
                DuplicationMeshes = new List<DuplicationMeshConfigData> {
                    new() {
                        MeshPath = "DefaultTie.obj",
                        Step = 2,
                        Color = new Color(0.6f, 0.435f, 0.27f, 1)
                    }
                },
                ExtrusionMeshes = new List<ExtrusionMeshConfigData> {
                    new() {
                        MeshPath = "DefaultRail.obj",
                        Color = Color.white,
                        TexturePath = "DefaultRailTexture.png"
                    }
                },
                DuplicationGizmos = new List<DuplicationGizmoConfigData>(),
                ExtrusionGizmos = new List<ExtrusionGizmoConfigData>()
            };
        }
    }
}
