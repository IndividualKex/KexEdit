using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KexEdit.UI {
    public static class TrainStyleConfigManager {
        public static string TrainStylesPath => Path.Combine(Application.streamingAssetsPath, "TrainStyles");

        public static List<TrainStyleConfigInfo> GetAvailableConfigsWithNames() {
            var configs = new List<TrainStyleConfigInfo>();

            try {
                string trainStylesPath = Path.Combine(Application.streamingAssetsPath, "TrainStyles");

                if (!Directory.Exists(trainStylesPath)) {
                    Debug.LogWarning($"TrainStyles directory not found at {trainStylesPath}");
                    return configs;
                }

                string[] jsonFiles = Directory.GetFiles(trainStylesPath, "*.json");
                if (jsonFiles == null || jsonFiles.Length == 0) {
                    Debug.LogWarning("No JSON files found in TrainStyles directory");
                    return configs;
                }

                foreach (string file in jsonFiles) {
                    if (string.IsNullOrEmpty(file)) continue;

                    string fileName = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(fileName)) continue;

                    try {
                        string configText = File.ReadAllText(file);
                        if (string.IsNullOrEmpty(configText)) {
                            Debug.LogWarning($"Train config file {fileName} is empty");
                            continue;
                        }

                        var config = JsonUtility.FromJson<TrainStyleConfig>(configText);
                        if (config != null && !string.IsNullOrEmpty(config.Name)) {
                            configs.Add(new TrainStyleConfigInfo {
                                FileName = fileName,
                                DisplayName = config.Name
                            });
                        }
                        else {
                            Debug.LogWarning($"Invalid or incomplete train config in {fileName}");
                        }
                    }
                    catch (System.Exception e) {
                        Debug.LogError($"Failed to parse train config {fileName}: {e.Message}");
                    }
                }
            }
            catch (System.Exception e) {
                Debug.LogError($"Failed to scan TrainStyles directory: {e.Message}");
            }

            return configs;
        }

        public static void LoadConfig(string configFileName) {
            if (string.IsNullOrEmpty(configFileName)) {
                Debug.LogError("Train style config filename cannot be null or empty");
                return;
            }

            string configPath = Path.Combine(Application.streamingAssetsPath, "TrainStyles", configFileName);
        }

        public static void OpenTrainStylesFolder() {
            string path = Path.Combine(Application.streamingAssetsPath, "TrainStyles");

            if (!Directory.Exists(path)) {
                Debug.LogWarning($"TrainStyles directory does not exist at {path}");
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
                Debug.LogError($"Failed to open TrainStyles folder: {e.Message}");
            }
        }

        public static string RelativePath(string fileName) {
            return Path.Combine("TrainStyles", fileName);
        }

        public struct TrainStyleConfigInfo {
            public string FileName;
            public string DisplayName;

            public TrainStyleConfigInfo(string fileName, string displayName) {
                FileName = fileName;
                DisplayName = displayName;
            }
        }
    }
}
