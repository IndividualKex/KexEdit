using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TrainMeshLoadingSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = SystemAPI.QueryBuilder()
                .WithAll<LoadTrainMeshEvent>()
                .Build();

            RequireForUpdate(_query);
        }

        protected override void OnUpdate() {
            using var events = _query.ToEntityArray(Allocator.Temp);
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < events.Length; i++) {
                var entity = events[i];
                var evt = SystemAPI.GetComponent<LoadTrainMeshEvent>(entity);

                if (evt.Target == Entity.Null ||
                    !SystemAPI.HasBuffer<TrainCarReference>(evt.Target)) {
                    UnityEngine.Debug.LogError("LoadTrainMeshEvent missing Target entity");
                    ecb.DestroyEntity(entity);
                    continue;
                }

                Entity carEntity = Entity.Null;
                var trainCarBuffer = SystemAPI.GetBuffer<TrainCarReference>(evt.Target);
                for (int j = 0; j < trainCarBuffer.Length; j++) {
                    if (trainCarBuffer[j] == Entity.Null ||
                        !SystemAPI.HasComponent<TrainCarMeshReference>(trainCarBuffer[j])) continue;
                    carEntity = trainCarBuffer[j];
                    break;
                }
                if (carEntity == Entity.Null ||
                    !SystemAPI.HasComponent<TrainCarMeshReference>(carEntity)) {
                    UnityEngine.Debug.LogError("Target entity missing TrainMeshReference component");
                    ecb.DestroyEntity(entity);
                    continue;
                }

                ref var trainCarMeshReference = ref SystemAPI.GetComponentRW<TrainCarMeshReference>(carEntity).ValueRW;
                if (trainCarMeshReference.Value != Entity.Null) {
                    ecb.DestroyEntity(trainCarMeshReference.Value);
                }

                Entity trainMesh = EntityManager.Instantiate(evt.Train);
                trainCarMeshReference.Value = trainMesh;
                ecb.AddComponent(trainMesh, new TrainCarMesh { TrainCar = carEntity });

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
        }
    }
}
