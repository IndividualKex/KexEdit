using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Entities;

namespace KexEdit.UI {
    public static class CartStyleConfigManager {
        public struct CartStyleConfigInfo {
            public string fileName;
            public string displayName;
        }

        public static List<CartStyleConfigInfo> GetAvailableConfigsWithNames() {
            var configs = new List<CartStyleConfigInfo>();

            try {
                string cartStylesPath = Path.Combine(Application.streamingAssetsPath, "CartStyles");

                if (!Directory.Exists(cartStylesPath)) {
                    Debug.LogWarning($"CartStyles directory not found at {cartStylesPath}");
                    return configs;
                }

                string[] jsonFiles = Directory.GetFiles(cartStylesPath, "*.json");
                if (jsonFiles == null || jsonFiles.Length == 0) {
                    Debug.LogWarning("No JSON files found in CartStyles directory");
                    return configs;
                }

                foreach (string file in jsonFiles) {
                    if (string.IsNullOrEmpty(file)) continue;

                    string fileName = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(fileName)) continue;

                    try {
                        string configText = File.ReadAllText(file);
                        if (string.IsNullOrEmpty(configText)) {
                            Debug.LogWarning($"Cart config file {fileName} is empty");
                            continue;
                        }

                        var config = JsonUtility.FromJson<CartMeshConfig>(configText);
                        if (config != null && !string.IsNullOrEmpty(config.Name)) {
                            configs.Add(new CartStyleConfigInfo {
                                fileName = fileName,
                                displayName = config.Name
                            });
                        }
                        else {
                            Debug.LogWarning($"Invalid or incomplete cart config in {fileName}");
                        }
                    }
                    catch (System.Exception e) {
                        Debug.LogError($"Failed to parse cart config {fileName}: {e.Message}");
                    }
                }
            }
            catch (System.Exception e) {
                Debug.LogError($"Failed to scan CartStyles directory: {e.Message}");
            }

            return configs;
        }

        public static void LoadConfig(string configFileName) {
            if (string.IsNullOrEmpty(configFileName)) {
                Debug.LogError("Cart style config filename cannot be null or empty");
                return;
            }

            try {
                CartStylePreferences.CurrentCartStyle = configFileName;

                var world = World.DefaultGameObjectInjectionWorld;
                if (world?.EntityManager != null) {
                    var entityManager = world.EntityManager;
                    var loadEvent = entityManager.CreateEntity();
                    entityManager.AddComponentData(loadEvent, new LoadCartStyleConfigEvent { Value = true });
                }
                else {
                    Debug.LogWarning("Default world or EntityManager not available for cart style loading");
                }
            }
            catch (System.Exception e) {
                Debug.LogError($"Failed to load cart style config {configFileName}: {e.Message}");
            }
        }

        public static void OpenCartStyleFolder() {
            string path = Path.Combine(Application.streamingAssetsPath, "CartStyles");

            if (!Directory.Exists(path)) {
                Debug.LogWarning($"CartStyles directory does not exist at {path}");
                return;
            }

            try {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start("explorer.exe", path.Replace('/', '\\'));
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                System.Diagnostics.Process.Start("open", path);
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX  
                System.Diagnostics.Process.Start("xdg-open", path);
#else
                Debug.LogWarning("Opening folders is not supported on this platform");
#endif
            }
            catch (System.Exception e) {
                Debug.LogError($"Failed to open CartStyles folder: {e.Message}");
            }
        }
    }
}
