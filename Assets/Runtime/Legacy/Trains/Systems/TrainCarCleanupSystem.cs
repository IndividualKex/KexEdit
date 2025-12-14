using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    [BurstCompile]
    public partial struct TrainCarCleanupSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (trainCarReferenceBuffer, coaster, entity) in SystemAPI
                .Query<DynamicBuffer<TrainCarReference>, CoasterReference>()
                .WithEntityAccess()
            ) {
                for (int i = trainCarReferenceBuffer.Length - 1; i >= 0; i--) {
                    var trainCarEntity = trainCarReferenceBuffer[i];
                    if (trainCarEntity == Entity.Null) continue;

                    if (!IsValidTrainCar(ref state, trainCarEntity, coaster)) {
                        ecb.DestroyEntity(trainCarEntity);
                        trainCarReferenceBuffer.RemoveAtSwapBack(i);
                    }
                }

                if (trainCarReferenceBuffer.Length == 0) {
                    ecb.RemoveComponent<TrainCarReference>(entity);
                }
            }
            ecb.Playback(state.EntityManager);
        }

        private bool IsValidTrainCar(ref SystemState state, Entity trainCarEntity, Entity coaster) {
            if (!SystemAPI.HasComponent<TrainCar>(trainCarEntity))
                return false;

            if (!SystemAPI.HasComponent<TrainStyleReference>(coaster))
                return false;

            var trainStyleEntity = SystemAPI.GetComponent<TrainStyleReference>(coaster);
            if (!SystemAPI.HasComponent<TrainStyle>(trainStyleEntity))
                return false;

            var trainCar = SystemAPI.GetComponent<TrainCar>(trainCarEntity);
            var trainStyle = SystemAPI.GetComponent<TrainStyle>(trainStyleEntity);

            return trainStyle.Version == trainCar.TrainStyleVersion;
        }
    }
}
