using System;
using System.IO;
using UnityEngine;
using KexEdit.UI.Serialization;

namespace KexEdit.UI {
    public static class ProjectOperations {
        public static string CurrentFilePath { get; private set; }
        public static bool HasUnsavedChanges { get; private set; }

        public static event Action<string> FilePathChanged;
        public static event Action<bool> UnsavedChangesChanged;

        static ProjectOperations() {
            Undo.Recorded += MarkAsModified;
        }

        public static void MarkAsModified() {
            if (HasUnsavedChanges) return;
            HasUnsavedChanges = true;
            UnsavedChangesChanged?.Invoke(true);
        }

        public static void MarkAsSaved() {
            if (!HasUnsavedChanges) return;
            HasUnsavedChanges = false;
            UnsavedChangesChanged?.Invoke(false);
        }

        public static void CreateNewProject() {
            CurrentFilePath = null;
            SerializationSystem.Instance.DeserializeGraph(new byte[0]);
            Undo.Clear();
            HasUnsavedChanges = false;

            FilePathChanged?.Invoke(CurrentFilePath);
            UnsavedChangesChanged?.Invoke(false);
        }

        public static void OpenProject(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return;

            try {
                byte[] graphData = FileManager.LoadGraph(filePath);
                if (graphData == null || graphData.Length == 0) return;

                SerializationSystem.Instance.DeserializeGraph(graphData);
                CurrentFilePath = filePath;
                Undo.Clear();
                HasUnsavedChanges = false;

                FilePathChanged?.Invoke(CurrentFilePath);
                UnsavedChangesChanged?.Invoke(false);
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to open project: {ex.Message}");
                throw;
            }
        }

        public static void SaveProject(string filePath = null) {
            string targetPath = filePath ?? CurrentFilePath;

            if (string.IsNullOrEmpty(targetPath)) {
                throw new InvalidOperationException("No file path specified for save operation");
            }

            try {
                FileManager.SaveGraph(SerializationSystem.Instance.SerializeGraph(), targetPath);

                if (targetPath != CurrentFilePath) {
                    CurrentFilePath = targetPath;
                    FilePathChanged?.Invoke(CurrentFilePath);
                }

                HasUnsavedChanges = false;
                UnsavedChangesChanged?.Invoke(false);
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to save project: {ex.Message}");
                throw;
            }
        }

        public static string GetProjectDisplayName() {
            return string.IsNullOrEmpty(CurrentFilePath)
                ? "Untitled"
                : Path.GetFileNameWithoutExtension(CurrentFilePath);
        }

        public static string GetProjectTitle() {
            string title = GetProjectDisplayName();
            if (HasUnsavedChanges) title += "*";
            return title;
        }
    }
}
