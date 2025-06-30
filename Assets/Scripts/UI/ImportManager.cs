using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using GLTFast;
using SFB;

namespace KexEdit.UI {
    public static class ImportManager {
        public static void ShowGltfImportDialog(VisualElement root, Action<string> onSuccess = null) {
            var extensions = new ExtensionFilter[] {
                new("glTF (.glb/.gltf)", "glb", "gltf"),
                new("All Files", "*")
            };

            string path = FileManager.ShowOpenFileDialog(extensions);

            if (string.IsNullOrEmpty(path)) {
                Debug.Log("Import canceled or no file selected.");
                return;
            }

            onSuccess?.Invoke(path);
        }

        public static async void ImportGltfFileAsync(string path, Action<ManagedMesh> onSuccess = null) {
            var gltf = new GltfImport();
            bool success = await gltf.Load(path);

            if (!success) {
                Debug.LogError("Failed to load glTF.");
                return;
            }

            var rootGO = new GameObject($"Imported glTF: {Path.GetFileNameWithoutExtension(path)}");

            success = await gltf.InstantiateMainSceneAsync(rootGO.transform);
            var managedMesh = rootGO.AddComponent<ManagedMesh>();

            if (!success) {
                Debug.LogError("Failed to instantiate glTF.");
                GameObject.Destroy(rootGO);
            }
            else {
                onSuccess?.Invoke(managedMesh);
            }
        }
    }
}
