using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace KexEdit.UI {
    public readonly struct TrackMeshConfigInfo {
        public readonly string FileName;
        public readonly string DisplayName;

        public TrackMeshConfigInfo(string fileName, string displayName) {
            FileName = fileName;
            DisplayName = displayName;
        }
    }

    public static class TrackMeshConfigManager {
        private static string TrackMeshPath => Path.Combine(Application.streamingAssetsPath, "TrackStyles");

        public static string[] GetAvailableConfigs() {
            return GetAvailableConfigsWithNames().Select(info => info.FileName).ToArray();
        }

        public static TrackMeshConfigInfo[] GetAvailableConfigsWithNames() {
            try {
                if (!Directory.Exists(TrackMeshPath)) {
                    return Array.Empty<TrackMeshConfigInfo>();
                }

                return Directory.GetFiles(TrackMeshPath, "*.json")
                    .Where(IsValidConfigFile)
                    .Select(filePath => {
                        string fileName = Path.GetFileName(filePath);
                        string displayName = GetConfigDisplayName(filePath, fileName);
                        return new TrackMeshConfigInfo(fileName, displayName);
                    })
                    .OrderBy(info => info.DisplayName)
                    .ToArray();
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to get available configs: {ex.Message}");
                return Array.Empty<TrackMeshConfigInfo>();
            }
        }

        public static void LoadConfig(string configFileName) {
            if (string.IsNullOrEmpty(configFileName)) {
                Debug.LogWarning("Config file name is empty");
                return;
            }

            string configPath = Path.Combine(TrackMeshPath, configFileName);
            if (!IsValidConfigFile(configPath)) {
                Debug.LogError($"Config file is not valid or does not exist: {configFileName}");
                return;
            }

            try {
                TrackStylePreferences.CurrentTrackMesh = configFileName;

                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                var loadEntity = ecb.CreateEntity();
                ecb.AddComponent(loadEntity, new LoadTrackMeshConfigEvent {
                    ConfigPath = configFileName
                });
                ecb.Playback(entityManager);
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to load config {configFileName}: {ex.Message}");
            }
        }

        public static void OpenTrackMeshFolder() {
            try {
                string path = TrackMeshPath;
                if (!Directory.Exists(path)) {
                    Debug.LogWarning($"TrackMesh directory does not exist: {path}");
                    return;
                }

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
            catch (Exception ex) {
                Debug.LogError($"Failed to open track style folder: {ex.Message}");
            }
        }

        private static string GetConfigDisplayName(string filePath, string fileName) {
            try {
                string content = File.ReadAllText(filePath);
                var config = JsonUtility.FromJson<TrackStyleConfig>(content);

                if (config != null && !string.IsNullOrWhiteSpace(config.Name)) {
                    return config.Name;
                }
            }
            catch {
                // Fall through to default name
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static bool IsValidConfigFile(string filePath) {
            try {
                if (!File.Exists(filePath)) return false;

                string fileName = Path.GetFileName(filePath);

                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }

                var info = new FileInfo(filePath);
                if (info.Length == 0) return false;

                string content = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(content)) return false;

                var config = JsonUtility.FromJson<TrackStyleConfig>(content);
                return config != null;
            }
            catch {
                return false;
            }
        }
    }
}
