using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MeshDataCreationSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Render>()
                .WithNone<HasMeshDataTag>()
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
                var meshDataEntity = ecb.CreateEntity();
                ecb.AddComponent(meshDataEntity, new MeshData {
                    Entity = entity
                });
                ecb.AddComponent<HasMeshDataTag>(entity);
                ecb.SetName(meshDataEntity, "MeshData");
            }
            entities.Dispose();
            render.Dispose();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
