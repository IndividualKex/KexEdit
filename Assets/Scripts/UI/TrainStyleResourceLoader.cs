using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KexEdit.UI {
    public static class TrainStyleResourceLoader {
        public static TrainStyleConfig LoadConfig(string configPath) {
            string fullPath = Path.Combine(TrainStyleConfigManager.TrainStylesPath, configPath);

            TrainStyleConfig config;

            if (!File.Exists(fullPath)) {
                Debug.LogWarning($"Train style config not found at {fullPath}. Using default configuration.");
                config = CreateDefaultConfig();
            }
            else {
                try {
                    string configText = File.ReadAllText(fullPath);
                    config = JsonUtility.FromJson<TrainStyleConfig>(configText);
                }
                catch (System.Exception e) {
                    Debug.LogError($"Failed to parse TrainStyleConfig: {e.Message}. Using default configuration.");
                    config = CreateDefaultConfig();
                }
            }

            config.SourceFileName = configPath;
            return config;
        }

        private static TrainStyleConfig CreateDefaultConfig() {
            return new TrainStyleConfig {
                Name = "Classic",
                CarCount = 5,
                CarSpacing = 2.5f,
                DefaultCar = new TrainCarTemplate {
                    MeshPath = "StylizedCart.glb",
                    WheelAssemblies = new List<WheelAssemblyConfig> {
                        new() {
                            MeshPath = "StylizedCart_FrontWheelAssembly.glb",
                            Offset = 0f
                        },
                        new() {
                            MeshPath = "StylizedCart_BackWheelAssembly.glb",
                            Offset = -1.5f
                        }
                    }
                }
            };
        }
    }
}
