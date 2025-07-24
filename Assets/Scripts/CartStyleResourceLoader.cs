using UnityEngine;
using System.IO;

namespace KexEdit {
    public static class CartStyleResourceLoader {
        private static string CartStylePath => Path.Combine(Application.streamingAssetsPath, "CartStyles");

        public static CartMeshConfig LoadConfig(string configPath) {
            if (string.IsNullOrEmpty(configPath)) {
                Debug.LogWarning("Cart style config path is null or empty.");
                return null;
            }

            string fullPath = Path.Combine(CartStylePath, configPath);

            if (!File.Exists(fullPath)) {
                Debug.LogWarning($"Cart style config not found at {fullPath}.");
                return null;
            }

            try {
                string configText = File.ReadAllText(fullPath);
                if (string.IsNullOrEmpty(configText)) {
                    Debug.LogWarning($"Cart style config at {fullPath} is empty.");
                    return null;
                }

                var config = JsonUtility.FromJson<CartMeshConfig>(configText);
                if (config == null) {
                    Debug.LogWarning($"Failed to parse cart style config at {fullPath}.");
                    return null;
                }

                config.SourceFileName = configPath;
                return config;
            }
            catch (System.Exception e) {
                Debug.LogError($"Failed to load cart style config from {fullPath}: {e.Message}");
                return null;
            }
        }
    }
}
