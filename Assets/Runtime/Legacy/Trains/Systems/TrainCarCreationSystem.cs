using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TrainCarCreationSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (train, coaster, entity) in SystemAPI
                .Query<Train, CoasterReference>()
                .WithNone<TrainCarReference>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasComponent<TrainStyleReference>(coaster)) continue;

                Entity styleEntity = SystemAPI.GetComponent<TrainStyleReference>(coaster);
                if (!SystemAPI.ManagedAPI.HasComponent<TrainStyleManaged>(styleEntity)) continue;

                var style = SystemAPI.ManagedAPI.GetComponent<TrainStyleManaged>(styleEntity);
                if (style.Loading) continue;

                ecb.AddBuffer<TrainCarReference>(entity);

                for (int i = 0; i < style.Data.TrainCars.Count; i++) {
                    var trainCar = style.Data.TrainCars[i];
                    if (!style.Loaded.TryGetValue(trainCar.MeshPath, out var mesh)) continue;
                    var carEntity = EntityManager.Instantiate(mesh);
                    ecb.AddComponent(carEntity, new TrainCar {
                        Train = entity,
                        TrainStyleVersion = style.Data.Version
                    });
                    ecb.AppendToBuffer<TrainCarReference>(entity, carEntity);
                    ecb.AddBuffer<WheelAssemblyReference>(carEntity);
                    ecb.SetName(carEntity, "TrainCar");

                    for (int j = 0; j < trainCar.WheelAssemblies.Count; j++) {
                        var wheelAssembly = trainCar.WheelAssemblies[j];
                        if (!style.Loaded.TryGetValue(wheelAssembly.MeshPath, out var wheelAssemblyMesh)) continue;
                        var wheelAssemblyEntity = EntityManager.Instantiate(wheelAssemblyMesh);
                        ecb.AddComponent(wheelAssemblyEntity, new WheelAssembly {
                            TrainCar = carEntity,
                            Offset = wheelAssembly.Offset + trainCar.Offset
                        });
                        ecb.AddComponent(wheelAssemblyEntity, TrackFollower.Default);
                        ecb.AddComponent<CoasterReference>(wheelAssemblyEntity, coaster);
                        ecb.AddComponent<DistanceFollower>(wheelAssemblyEntity);
                        ecb.AppendToBuffer<WheelAssemblyReference>(carEntity, wheelAssemblyEntity);
                        ecb.SetName(wheelAssemblyEntity, "WheelAssembly");
                    }
                }
            }
            ecb.Playback(EntityManager);
        }
    }
}
