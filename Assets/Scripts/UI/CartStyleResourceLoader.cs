using UnityEngine;
using System.IO;

namespace KexEdit.UI {
    public static class TrainStyleResourceLoader {
        private static string TrainStylePath => Path.Combine(Application.streamingAssetsPath, "TrainStyles");

        public static TrainStyleConfig LoadConfig(string configPath) {
            if (string.IsNullOrEmpty(configPath)) {
                Debug.LogWarning("Train style config path is null or empty.");
                return null;
            }

            string fullPath = Path.Combine(TrainStylePath, configPath);

            if (!File.Exists(fullPath)) {
                Debug.LogWarning($"Train style config not found at {fullPath}.");
                return null;
            }

            try {
                string configText = File.ReadAllText(fullPath);
                if (string.IsNullOrEmpty(configText)) {
                    Debug.LogWarning($"Train style config at {fullPath} is empty.");
                    return null;
                }

                var config = JsonUtility.FromJson<TrainStyleConfig>(configText);
                if (config == null) {
                    Debug.LogWarning($"Failed to parse train style config at {fullPath}.");
                    return null;
                }

                config.SourceFileName = configPath;
                return config;
            }
            catch (System.Exception e) {
                Debug.LogError($"Failed to load train style config from {fullPath}: {e.Message}");
                return null;
            }
        }
    }
}
