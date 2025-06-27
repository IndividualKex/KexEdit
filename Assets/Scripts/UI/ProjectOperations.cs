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

                Preferences.AddRecentFile(filePath);
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

                Preferences.AddRecentFile(targetPath);
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

        public static string GetLatestRecoveryFile() {
            try {
                string directory = Path.Combine(Application.persistentDataPath, "Tracks");
                if (!Directory.Exists(directory)) return null;

                string[] recoveryFiles = Directory.GetFiles(directory, "*.kex1");
                if (recoveryFiles.Length == 0) return null;

                Array.Sort(recoveryFiles, (x, y) => File.GetLastWriteTime(y).CompareTo(File.GetLastWriteTime(x)));
                return recoveryFiles[0];
            }
            catch {
                return null;
            }
        }

        public static void RecoverLastSession() {
            try {
                string directory = Path.Combine(Application.persistentDataPath, "Tracks");
                if (!Directory.Exists(directory)) {
                    CurrentFilePath = null;
                    FilePathChanged?.Invoke(CurrentFilePath);
                    return;
                }

                string[] recentFiles = Preferences.RecentFiles;
                if (recentFiles.Length == 0) {
                    string untitledRecovery = Path.Combine(directory, "untitled.kex1");
                    if (File.Exists(untitledRecovery)) {
                        OpenRecoveryFile(untitledRecovery);
                    }
                    return;
                }

                for (int i = 0; i < recentFiles.Length; i++) {
                    string projectFile = recentFiles[i];
                    if (!File.Exists(projectFile)) continue;

                    string recoveryFile = Path.Combine(directory, Path.GetFileNameWithoutExtension(projectFile) + ".kex1");

                    if (File.Exists(recoveryFile)) {
                        DateTime projectTime = File.GetLastWriteTime(projectFile);
                        DateTime recoveryTime = File.GetLastWriteTime(recoveryFile);

                        if (recoveryTime > projectTime) {
                            OpenRecoveryFile(recoveryFile);
                        }
                        else {
                            OpenProject(projectFile);
                        }
                        return;
                    }
                    else {
                        OpenProject(projectFile);
                        return;
                    }
                }
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to recover last session: {ex.Message}");
            }

            CurrentFilePath = null;
            FilePathChanged?.Invoke(CurrentFilePath);
        }

        private static void OpenRecoveryFile(string recoveryFilePath) {
            try {
                byte[] graphData = FileManager.LoadGraph(recoveryFilePath);
                if (graphData == null || graphData.Length == 0) return;

                SerializationSystem.Instance.DeserializeGraph(graphData);

                string fileName = Path.GetFileName(recoveryFilePath);
                CurrentFilePath = fileName == "untitled.kex1" ? null :
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    fileName[..^1]);

                Undo.Clear();
                HasUnsavedChanges = false;
                FilePathChanged?.Invoke(CurrentFilePath);
                UnsavedChangesChanged?.Invoke(false);
            }
            catch (Exception ex) {
                Debug.LogError($"Failed to open recovery file: {ex.Message}");
                throw;
            }
        }
    }
}
