using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ColliderDataCreationSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackPoint, Render>()
                .WithNone<HasColliderDataTag>()
                .Build(EntityManager);

            RequireForUpdate(_query);
        }

        protected override void OnUpdate() {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var entities = _query.ToEntityArray(Allocator.Temp);
            var render = _query.ToComponentDataArray<Render>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++) {
                if (!render[i]) continue;

                var entity = entities[i];
                ecb.AddComponent<ColliderHash>(entity);
                ecb.AddBuffer<ColliderReference>(entity);
                ecb.AddComponent<HasColliderDataTag>(entity);
            }
            entities.Dispose();
            render.Dispose();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
