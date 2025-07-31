using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using KexEdit.Serialization;
using Unity.Entities;

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

        public static Entity CreateNewProject() {
            CurrentFilePath = null;
            Entity coaster = SerializationSystem.Instance.DeserializeGraph(new byte[0]);
            Undo.Clear();
            HasUnsavedChanges = false;

            FilePathChanged?.Invoke(CurrentFilePath);
            UnsavedChangesChanged?.Invoke(false);

            return coaster;
        }

        public static Entity OpenProject(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return Entity.Null;

            try {
                byte[] graphData = File.ReadAllBytes(filePath);
                if (graphData == null || graphData.Length == 0) return Entity.Null;

                Entity coaster = SerializationSystem.Instance.DeserializeGraph(graphData);

                if (filePath.EndsWith(".kex1")) {
                    string fileName = Path.GetFileName(filePath);
                    CurrentFilePath = fileName == "untitled.kex1" ? null :
                        Path.Combine(Application.persistentDataPath, "Tracks", fileName[..^1]);
                }
                else {
                    CurrentFilePath = filePath;
                }

                Undo.Clear();
                HasUnsavedChanges = false;
                FilePathChanged?.Invoke(CurrentFilePath);
                UnsavedChangesChanged?.Invoke(false);

                return coaster;
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
                FileManager.SaveGraph(ProjectOperationsSystem.Instance.SerializeGraph(), targetPath);

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

        public static string GetLatestRecoveryFile() {
            try {
                string directory = Path.Combine(Application.persistentDataPath, "Tracks");
                if (!Directory.Exists(directory)) return null;

                string[] recoveryFiles = Directory.GetFiles(directory, "*.kex1");
                if (recoveryFiles.Length == 0) return null;

                string latestFile = null;
                DateTime latestTime = DateTime.MinValue;

                foreach (string file in recoveryFiles) {
                    if (IsValidFile(file)) {
                        DateTime lastWrite = File.GetLastWriteTime(file);
                        if (lastWrite > latestTime) {
                            latestTime = lastWrite;
                            latestFile = file;
                        }
                    }
                }

                return latestFile;
            }
            catch {
                return null;
            }
        }

        public static string[] GetRecentValidFiles(int maxCount = 5) {
            var recentFiles = new List<(string path, DateTime time)>();

            string dir = Path.Combine(Application.persistentDataPath, "Tracks");

            if (Directory.Exists(dir)) {
                try {
                    string[] kexFiles = Directory.GetFiles(dir, "*.kex", SearchOption.AllDirectories);
                    foreach (string file in kexFiles) {
                        if (IsValidFile(file)) {
                            DateTime lastWrite = File.GetLastWriteTime(file);
                            recentFiles.Add((file, lastWrite));
                        }
                    }
                }
                catch (Exception ex) {
                    Debug.LogWarning($"Failed to search directory {dir}: {ex.Message}");
                }
            }

            return recentFiles
                .OrderByDescending(f => f.time)
                .Take(maxCount)
                .Select(f => f.path)
                .ToArray();
        }

        public static string FindMostRecentValidFile() {
            string dir = Path.Combine(Application.persistentDataPath, "Tracks");
            if (!Directory.Exists(dir)) return null;

            try {
                string mostRecentFile = null;
                DateTime mostRecentTime = DateTime.MinValue;

                foreach (string file in Directory.GetFiles(dir, "*.kex*")) {
                    if ((file.EndsWith(".kex") || file.EndsWith(".kex1")) && IsValidFile(file)) {
                        DateTime lastWrite = File.GetLastWriteTime(file);
                        if (lastWrite > mostRecentTime) {
                            mostRecentTime = lastWrite;
                            mostRecentFile = file;
                        }
                    }
                }

                return mostRecentFile;
            }
            catch (Exception ex) {
                Debug.LogWarning($"Failed to find most recent valid file: {ex.Message}");
                return null;
            }
        }

        private static bool IsValidFile(string filePath) {
            try {
                if (!File.Exists(filePath)) return false;
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0) return false;
                if (DateTime.Now - fileInfo.LastWriteTime > TimeSpan.FromDays(30)) return false;
                return IsValidFileHeader(filePath);
            }
            catch (Exception ex) {
                Debug.LogWarning($"Failed to validate file {filePath}: {ex.Message}");
                return false;
            }
        }

        private static bool IsValidFileHeader(string filePath) {
            try {
                using var stream = File.OpenRead(filePath);
                if (stream.Length < 4) return false;
                var buffer = new byte[4];
                stream.Read(buffer, 0, 4);
                int version = BitConverter.ToInt32(buffer, 0);
                return version > 0 && version <= 10;
            }
            catch {
                return false;
            }
        }
    }
}
