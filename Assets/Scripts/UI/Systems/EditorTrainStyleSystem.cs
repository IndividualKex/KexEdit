using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

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
                Version = version,
                CarCount = config.CarCount,
                CarSpacing = config.CarSpacing,
                DefaultCar = ConvertCarTemplate(config.DefaultCar),
                CarOverrides = ConvertCarOverrides(config.CarOverrides)
            };

            if (config.TrainCars != null && config.TrainCars.Count > 0) {
                foreach (var trainCar in config.TrainCars) {
                    string trainCarPath = TrainStyleConfigManager.RelativePath(trainCar.MeshPath);
                    var trainCarData = new TrainCarData {
                        MeshPath = trainCarPath,
                        Offset = trainCar.Offset
                    };
                    data.TrainCars.Add(trainCarData);

                    foreach (var wheelAssembly in trainCar.WheelAssemblies) {
                        string wheelAssemblyPath = TrainStyleConfigManager.RelativePath(wheelAssembly.MeshPath);
                        trainCarData.WheelAssemblies.Add(new WheelAssemblyData {
                            MeshPath = wheelAssemblyPath,
                            Offset = wheelAssembly.Offset
                        });
                    }
                }
            }
            else {
                GenerateCarsFromTemplate(data);
            }

            return data;
        }

        private void GenerateCarsFromTemplate(TrainStyleData data) {
            int carCount = TrainCarCountPreferences.GetCarCount(Preferences.CurrentTrainStyle, data.CarCount);
            
            if (carCount <= 0 || data.DefaultCar == null) return;

            float totalSpacing = (carCount - 1) * data.CarSpacing;
            float startOffset = totalSpacing * 0.5f;

            for (int i = 0; i < carCount; i++) {
                float offset = startOffset - (i * data.CarSpacing);

                var template = data.DefaultCar;
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

                ApplyCarOverride(data, carCount, i, carData);
                data.TrainCars.Add(carData);
            }
        }

        private void ApplyCarOverride(TrainStyleData data, int carCount, int carIndex, TrainCarData carData) {
            if (data.CarOverrides == null) return;

            foreach (var carOverride in data.CarOverrides) {
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

        private TrainCarTemplateData ConvertCarTemplate(TrainCarTemplate template) {
            if (template == null) return null;
            
            return new TrainCarTemplateData {
                MeshPath = template.MeshPath,
                WheelAssemblies = ConvertWheelAssemblyTemplates(template.WheelAssemblies)
            };
        }

        private List<TrainCarOverrideData> ConvertCarOverrides(List<TrainCarOverride> overrides) {
            if (overrides == null) return null;
            
            var result = new List<TrainCarOverrideData>();
            foreach (var carOverride in overrides) {
                result.Add(new TrainCarOverrideData {
                    Index = carOverride.Index,
                    MeshPath = carOverride.MeshPath,
                    WheelAssemblies = ConvertWheelAssemblyTemplates(carOverride.WheelAssemblies)
                });
            }
            return result;
        }

        private List<WheelAssemblyTemplateData> ConvertWheelAssemblyTemplates(List<WheelAssemblyConfig> configs) {
            if (configs == null) return null;
            
            var result = new List<WheelAssemblyTemplateData>();
            foreach (var config in configs) {
                result.Add(new WheelAssemblyTemplateData {
                    MeshPath = config.MeshPath,
                    Offset = config.Offset
                });
            }
            return result;
        }
    }
}
