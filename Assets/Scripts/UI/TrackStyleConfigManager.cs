using System;
using System.IO;
using System.Linq;
using UnityEngine;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public static class TrackStyleConfigManager {
        public static string TrackStylesPath => Path.Combine(Application.streamingAssetsPath, "TrackStyles");

        public static string[] GetAvailableConfigs() {
            return GetAvailableConfigsWithNames().Select(info => info.FileName).ToArray();
        }

        public static TrackStyleConfigInfo[] GetAvailableConfigsWithNames() {
            try {
                if (!Directory.Exists(TrackStylesPath)) {
                    return Array.Empty<TrackStyleConfigInfo>();
                }

                return Directory.GetFiles(TrackStylesPath, "*.json")
                    .Where(IsValidConfigFile)
                    .Select(filePath => {
                        string fileName = Path.GetFileName(filePath);
                        string displayName = GetConfigDisplayName(filePath, fileName);
                        return new TrackStyleConfigInfo(fileName, displayName);
                    })
                    .OrderBy(info => info.DisplayName)
                    .ToArray();
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to get available configs: {ex.Message}");
                return Array.Empty<TrackStyleConfigInfo>();
            }
        }

        public static TrackStyleConfig LoadConfig(string filename) {
            if (string.IsNullOrEmpty(filename)) {
                Debug.LogWarning("Config file name is empty");
                return null;
            }

            string configPath = Path.Combine(TrackStylesPath, filename);
            if (!IsValidConfigFile(configPath)) {
                Debug.LogError($"Config file is not valid or does not exist: {filename}");
                return null;
            }

            try {
                return TrackStyleResourceLoader.LoadConfig(filename);
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to load config {filename}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        public static void OpenTrackStylesFolder() {
            try {
                string path = TrackStylesPath;
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

        public struct TrackStyleConfigInfo {
            public string FileName;
            public string DisplayName;

            public TrackStyleConfigInfo(string fileName, string displayName) {
                FileName = fileName;
                DisplayName = displayName;
            }
        }
    }
}
