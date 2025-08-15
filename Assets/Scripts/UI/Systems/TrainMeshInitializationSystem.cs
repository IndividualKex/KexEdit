using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    [UpdateAfter(typeof(TrainStyleAssetLoadingSystem))]
    public partial class TrainMeshInitializationSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrainStyleReference, TrainCarMeshReference, CoasterReference>()
                .Build();

            RequireForUpdate(_query);
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<TrainStyleSettings>();
        }

        protected override void OnUpdate() {
            var globalSettings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            var styleSettings = SystemAPI.ManagedAPI.GetSingleton<TrainStyleSettings>();

            using var entities = _query.ToEntityArray(Allocator.Temp);

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++) {
                var entity = entities[i];

                ref var style = ref SystemAPI.GetComponentRW<TrainStyleReference>(entity).ValueRW;
                var coaster = SystemAPI.GetComponent<CoasterReference>(entity);

                if (!SystemAPI.HasComponent<EditorCoasterTag>(coaster) ||
                    style.Version == styleSettings.Version) continue;

                ref var mesh = ref SystemAPI.GetComponentRW<TrainCarMeshReference>(entity).ValueRW;

                if (mesh.Value != Entity.Null) {
                    ecb.DestroyEntity(mesh.Value);
                    mesh.Value = Entity.Null;
                }

                var trainStyle = styleSettings.Styles[style.StyleIndex];
                if (trainStyle.Mesh == Entity.Null) continue;

                Entity instance = EntityManager.Instantiate(trainStyle.Mesh);
                ecb.AddComponent(instance, new TrainCarMesh { TrainCar = entity });
                mesh.Value = instance;

                if (SystemAPI.HasComponent<RenderTag>(entity)) {
                    var renderTag = SystemAPI.GetComponent<RenderTag>(entity);
                    if ((renderTag.Type & RenderTagType.Playhead) != 0) {
                        var playheadGizmoMaterial = Resources.Load<Material>("PlayheadGizmo");
                        ecb.AddComponent(instance, new PendingMaterialUpdate { Material = playheadGizmoMaterial });
                    }
                }

                style.Version = styleSettings.Version;
            }
            ecb.Playback(EntityManager);
        }
    }
}
