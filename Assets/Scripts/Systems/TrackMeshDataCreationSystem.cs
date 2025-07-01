using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TrackMeshDataCreationSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Render>()
                .WithNone<HasTrackMeshDataTag>()
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
                var trackMeshDataEntity = ecb.CreateEntity();
                ecb.AddComponent(trackMeshDataEntity, new TrackMeshData {
                    Entity = entity
                });
                ecb.AddComponent<HasTrackMeshDataTag>(entity);
                ecb.SetName(trackMeshDataEntity, "TrackMeshData");
            }
            entities.Dispose();
            render.Dispose();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
