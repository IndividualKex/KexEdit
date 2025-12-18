using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class EditorTrainStyleSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<EditorTrainStyleSingleton>();
        }

        protected override void OnUpdate() {
            UpdateAssignments();
            UpdateSettings();
        }

        private void UpdateAssignments() {
            var singleton = SystemAPI.GetSingleton<EditorTrainStyleSingleton>();

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            if (SystemAPI.HasComponent<TrainStyle>(singleton.Style)) {
                foreach (var (_, coaster) in SystemAPI.Query<Coaster>().WithAll<EditorCoasterTag>().WithEntityAccess()) {
                    if (SystemAPI.HasComponent<TrainStyleReference>(coaster)) {
                        var settings = SystemAPI.GetComponent<TrainStyleReference>(coaster);
                        if (settings.Value.Equals(singleton.Style)) continue;
                        var settingsRW = SystemAPI.GetComponentRW<TrainStyleReference>(coaster);
                        settingsRW.ValueRW.Value = singleton.Style;
                    }
                    else {
                        ecb.AddComponent<TrainStyleReference>(coaster, singleton.Style);
                    }
                }
            }
            ecb.Playback(EntityManager);
        }

        private void UpdateSettings() {
            var singletonRW = SystemAPI.GetSingletonRW<EditorTrainStyleSingleton>();

            if (!singletonRW.ValueRO.Dirty) return;
            singletonRW.ValueRW.Dirty = false;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            int version = 0;
            if (SystemAPI.HasComponent<TrainStyle>(singletonRW.ValueRO.Style)) {
                var settings = SystemAPI.GetComponent<TrainStyle>(singletonRW.ValueRO.Style);
                version = settings.Version + 1;
                if (SystemAPI.ManagedAPI.HasComponent<TrainStyleManaged>(singletonRW.ValueRO.Style)) {
                    var managed = SystemAPI.ManagedAPI.GetComponent<TrainStyleManaged>(singletonRW.ValueRO.Style);
                    managed.Dispose(ecb);
                }
                ecb.DestroyEntity(singletonRW.ValueRO.Style);
                singletonRW.ValueRW.Style = Entity.Null;
            }

            var styleEntity = EntityManager.CreateEntity();
            singletonRW.ValueRW.Style = styleEntity;

            var config = TrainStyleResourceLoader.LoadConfig(Preferences.CurrentTrainStyle);
            var data = ConvertConfigToData(config, version);
            ecb.AddComponent(styleEntity, new LoadTrainStyleEvent {
                Data = data
            });
            ecb.SetName(styleEntity, "Train Style");

            ecb.Playback(EntityManager);
        }

        private TrainStyleData ConvertConfigToData(TrainStyleConfig config, int version) {
            var data = new TrainStyleData {
                TrainCars = new List<TrainCarData>(),
                Version = version
            };

            GenerateCarsFromTemplate(data, config);

            return data;
        }

        private void GenerateCarsFromTemplate(TrainStyleData data, TrainStyleConfig config) {
            int carCount = TrainCarCountPreferences.GetCarCount(Preferences.CurrentTrainStyle, config.CarCount);

            if (carCount <= 0 || config.DefaultCar == null) return;

            float totalSpacing = (carCount - 1) * config.CarSpacing;
            float startOffset = totalSpacing * 0.5f;

            for (int i = 0; i < carCount; i++) {
                float offset = startOffset - (i * config.CarSpacing);

                var template = config.DefaultCar;
                var carData = new TrainCarData {
                    MeshPath = TrainStyleConfigManager.RelativePath(template.MeshPath),
                    Offset = offset
                };

                if (template.WheelAssemblies != null) {
                    foreach (var wheelAssembly in template.WheelAssemblies) {
                        string wheelAssemblyPath = TrainStyleConfigManager.RelativePath(wheelAssembly.MeshPath);
                        carData.WheelAssemblies.Add(new WheelAssemblyData {
                            MeshPath = wheelAssemblyPath,
                            Offset = wheelAssembly.Offset
                        });
                    }
                }

                ApplyCarOverride(config, carCount, i, carData);
                data.TrainCars.Add(carData);
            }
        }

        private void ApplyCarOverride(TrainStyleConfig config, int carCount, int carIndex, TrainCarData carData) {
            if (config.CarOverrides == null) return;

            foreach (var carOverride in config.CarOverrides) {
                int targetIndex = carOverride.Index;
                if (targetIndex < 0) {
                    targetIndex = carCount + targetIndex;
                }

                if (targetIndex < 0 || targetIndex >= carCount || targetIndex != carIndex) continue;

                if (!string.IsNullOrEmpty(carOverride.MeshPath)) {
                    carData.MeshPath = TrainStyleConfigManager.RelativePath(carOverride.MeshPath);
                }

                if (carOverride.WheelAssemblies != null) {
                    carData.WheelAssemblies.Clear();
                    foreach (var wheelAssembly in carOverride.WheelAssemblies) {
                        string wheelAssemblyPath = TrainStyleConfigManager.RelativePath(wheelAssembly.MeshPath);
                        carData.WheelAssemblies.Add(new WheelAssemblyData {
                            MeshPath = wheelAssemblyPath,
                            Offset = wheelAssembly.Offset
                        });
                    }
                }
                break;
            }
        }

    }
}
