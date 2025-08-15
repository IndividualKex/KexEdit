using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class RecoverySystem : SystemBase {
        private const float RECOVERY_INTERVAL = 10f;

        private float _timeSinceLastSave;

        protected override void OnUpdate() {
            _timeSinceLastSave += SystemAPI.Time.DeltaTime;

            if (_timeSinceLastSave >= RECOVERY_INTERVAL) {
                PerformRecoverySave();
                _timeSinceLastSave = 0f;
            }
        }

        private void PerformRecoverySave() {
            try {
                string directory = Path.Combine(Application.persistentDataPath, "Tracks");
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                string fileName = GetRecoveryFileName();
                string filePath = Path.Combine(directory, fileName);

                byte[] graphData = ProjectOperationsSystem.Instance.SerializeGraph();
                File.WriteAllBytes(filePath, graphData);

                Cleanup(directory);
            }
            catch (Exception ex) {
                Debug.LogError($"Recovery save failed: {ex.Message}");
            }
        }

        private string GetRecoveryFileName() {
            string currentFile = ProjectOperations.CurrentFilePath;
            return !string.IsNullOrEmpty(currentFile)
                ? Path.GetFileNameWithoutExtension(currentFile) + ".kex1"
                : "untitled.kex1";
        }

        private void Cleanup(string directory) {
            try {
                string[] recoveryFiles = Directory.GetFiles(directory, "*.kex1");
                if (recoveryFiles.Length <= 1) return;

                var fileGroups = new NativeParallelMultiHashMap<FixedString128Bytes, int>(recoveryFiles.Length, Allocator.Temp);
                var processed = new NativeHashSet<FixedString128Bytes>(16, Allocator.Temp);

                for (int i = 0; i < recoveryFiles.Length; i++) {
                    fileGroups.Add(Path.GetFileNameWithoutExtension(recoveryFiles[i]), i);
                }

                for (int i = 0; i < recoveryFiles.Length; i++) {
                    var baseName = new FixedString128Bytes(Path.GetFileNameWithoutExtension(recoveryFiles[i]));
                    if (!processed.Add(baseName)) continue;

                    var indices = new NativeList<int>(Allocator.Temp);
                    if (fileGroups.TryGetFirstValue(baseName, out int idx, out var it)) {
                        indices.Add(idx);
                        while (fileGroups.TryGetNextValue(out idx, ref it)) indices.Add(idx);
                    }

                    if (indices.Length > 1) {
                        int keepIndex = indices[0];
                        var keepTime = File.GetCreationTime(recoveryFiles[keepIndex]);

                        for (int j = 1; j < indices.Length; j++) {
                            var time = File.GetCreationTime(recoveryFiles[indices[j]]);
                            if (time > keepTime) {
                                File.Delete(recoveryFiles[keepIndex]);
                                keepIndex = indices[j];
                                keepTime = time;
                            }
                            else {
                                File.Delete(recoveryFiles[indices[j]]);
                            }
                        }
                    }
                    indices.Dispose();
                }

                fileGroups.Dispose();
                processed.Dispose();
            }
            catch (Exception ex) {
                Debug.LogWarning($"Failed to cleanup old recovery files: {ex.Message}");
            }
        }
    }
}
