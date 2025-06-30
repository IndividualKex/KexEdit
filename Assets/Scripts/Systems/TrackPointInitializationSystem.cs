using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct TrackPointInitializationSystem : ISystem {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state) {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Point>()
                .WithNone<TrackPoint>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var entities = _query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++) {
                var entity = entities[i];
                ecb.AddBuffer<TrackPoint>(entity);
                ecb.AddComponent<TrackHash>(entity);
                ecb.AddComponent<SelectedBlend>(entity);
            }
            entities.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
