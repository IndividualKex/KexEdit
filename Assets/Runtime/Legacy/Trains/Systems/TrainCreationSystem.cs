using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct TrainCreationSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (coaster, entity) in SystemAPI
                .Query<Coaster>()
                .WithNone<TrainReference>()
                .WithEntityAccess()
            ) {
                var trainEntity = ecb.CreateEntity();
                ecb.AddComponent(trainEntity, LocalTransform.Identity);
                ecb.AddComponent<TrainReference>(entity, trainEntity);
                ecb.AddComponent<CoasterReference>(trainEntity, entity);
                ecb.AddComponent(trainEntity, new Train { Enabled = true, Kinematic = false });
                ecb.AddComponent(trainEntity, TrackFollower.Default);
                ecb.SetName(trainEntity, "Train");
            }
            ecb.Playback(state.EntityManager);
        }
    }
}
