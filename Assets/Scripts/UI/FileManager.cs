using SFB;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

namespace KexEdit.UI {
    public static class FileManager {
        private const string FileExtension = ".kex";
        private static string DefaultDirectory => Path.Combine(Application.persistentDataPath, "Tracks");

        static FileManager() {
            if (!Directory.Exists(DefaultDirectory)) {
                Directory.CreateDirectory(DefaultDirectory);
            }
        }

        public static void SaveGraph(byte[] graphData, string filePath = null) {
            try {
                if (string.IsNullOrEmpty(filePath)) {
                    filePath = Path.Combine(DefaultDirectory,
                        $"Track_{DateTime.Now:yyyyMMdd_HHmmss}{FileExtension}");
                }

                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(filePath, graphData);
            }
            catch (Exception ex) {
                Debug.LogError($"Error saving graph: {ex.Message}");
                throw;
            }
        }

        public static byte[] LoadGraph(string filePath) {
            try {
                if (!File.Exists(filePath)) {
                    Debug.LogError($"File not found: {filePath}");
                    return null;
                }

                return File.ReadAllBytes(filePath);
            }
            catch (Exception ex) {
                Debug.LogError($"Error loading graph: {ex.Message}");
                throw;
            }
        }

        public static string ShowSaveFileDialog(string defaultName = null) {
            defaultName ??= $"Track_{DateTime.Now:yyyyMMdd_HHmmss}";

            var extensionList = new[] {
                new ExtensionFilter("KexEdit Files", FileExtension.TrimStart('.')),
                new ExtensionFilter("All Files", "*")
            };

            string filePath = StandaloneFileBrowser.SaveFilePanel(
                "Save Track",
                DefaultDirectory,
                defaultName,
                extensionList);

            return string.IsNullOrEmpty(filePath) ? null : filePath;
        }

        public static string ShowOpenFileDialog() {
            var extensionList = new[] {
                new ExtensionFilter("KexEdit Files", FileExtension.TrimStart('.')),
                new ExtensionFilter("All Files", "*")
            };

            var paths = StandaloneFileBrowser.OpenFilePanel(
                "Open Track",
                DefaultDirectory,
                extensionList,
                false);

            return paths.Length > 0 ? paths[0] : null;
        }

        public static string ShowOpenFileDialog(List<ExtensionFilter> extensionFilters) {
            var paths = StandaloneFileBrowser.OpenFilePanel(
                    "Open File",
                    DefaultDirectory,
                    extensionFilters.ToArray(),
                    false);

            return paths.Length > 0 ? paths[0] : null;
        }

        public static void ShowSaveFileDialogAsync(string defaultName, System.Action<string> callback) {
            defaultName ??= $"Track_{DateTime.Now:yyyyMMdd_HHmmss}";

            var extensionList = new[] {
                new ExtensionFilter("KexEdit Files", FileExtension.TrimStart('.')),
                new ExtensionFilter("All Files", "*")
            };

            StandaloneFileBrowser.SaveFilePanelAsync(
                "Save Track",
                DefaultDirectory,
                defaultName,
                extensionList,
                callback);
        }

        public static void ShowOpenFileDialogAsync(System.Action<string> callback) {
            var extensionList = new[] {
                new ExtensionFilter("KexEdit Files", FileExtension.TrimStart('.')),
                new ExtensionFilter("All Files", "*")
            };

            StandaloneFileBrowser.OpenFilePanelAsync(
                "Open Track",
                DefaultDirectory,
                extensionList,
                false,
                (string[] paths) => {
                    callback?.Invoke(paths.Length > 0 ? paths[0] : null);
                });
        }
    }
}
