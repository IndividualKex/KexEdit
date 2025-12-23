using System.IO;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class AppendLoadingSystem : SystemBase {
        protected override void OnUpdate() {
            using var pending = new NativeList<PendingLoad>(Allocator.Temp);

            foreach (var (appendReferenceRW, entity) in SystemAPI
                .Query<RefRW<AppendReference>>()
                .WithEntityAccess()
            ) {
                if (appendReferenceRW.ValueRO.Loaded ||
                    appendReferenceRW.ValueRO.FilePath.IsEmpty ||
                    appendReferenceRW.ValueRO.Value != Entity.Null) continue;

                ref var appendReference = ref appendReferenceRW.ValueRW;
                appendReference.Loaded = true;
                string filePath = appendReference.FilePath.ToString();

                if (!filePath.EndsWith(".kex")) {
                    Debug.LogError($"Unsupported file type for Append node: {filePath}");
                    continue;
                }

                if (!File.Exists(filePath)) {
                    Debug.LogError($"File not found for Append node: {filePath}");
                    continue;
                }

                try {
                    byte[] graphData = File.ReadAllBytes(filePath);
                    if (graphData == null || graphData.Length == 0) {
                        Debug.LogError($"Empty or invalid file for Append node: {filePath}");
                        continue;
                    }

                    var nativeData = new NativeArray<byte>(graphData, Allocator.Temp);
                    pending.Add(new PendingLoad {
                        Entity = entity,
                        FilePath = filePath,
                        Data = nativeData
                    });
                }
                catch (System.Exception ex) {
                    Debug.LogError($"Error reading Append node file {filePath}: {ex.Message}");
                }
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < pending.Length; i++) {
                var item = pending[i];
                try {
                    var managedData = item.Data.ToArray();
                    Entity coaster = SerializationSystem.Instance.DeserializeGraph(managedData, restoreUIState: false);
                    if (coaster == Entity.Null) {
                        Debug.LogError($"Failed to deserialize coaster from: {item.FilePath}");
                        continue;
                    }

                    ecb.AddComponent<AppendedCoasterTag>(coaster);
                    var appendReference = SystemAPI.GetComponentRW<AppendReference>(item.Entity);
                    appendReference.ValueRW.Value = coaster;
                }
                catch (System.Exception ex) {
                    Debug.LogError($"Error loading Append node file {item.FilePath}: {ex.Message}");
                }
                finally {
                    item.Data.Dispose();
                }
            }
            ecb.Playback(EntityManager);
        }

        private struct PendingLoad {
            public Entity Entity;
            public FixedString512Bytes FilePath;
            public NativeArray<byte> Data;
        }
    }
}
