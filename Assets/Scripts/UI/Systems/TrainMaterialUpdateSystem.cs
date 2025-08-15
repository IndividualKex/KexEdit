using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrainMaterialUpdateSystem : SystemBase {
        private EntityQuery _query;

        protected override void OnCreate() {
            _query = SystemAPI.QueryBuilder()
                .WithAll<PendingMaterialUpdate>()
                .Build();

            RequireForUpdate(_query);
        }

        protected override void OnUpdate() {
            using var entities = _query.ToEntityArray(Allocator.Temp);
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var entity in entities) {
                var materialUpdate = SystemAPI.ManagedAPI.GetComponent<PendingMaterialUpdate>(entity);
                if (TryUpdateTrain(entity, materialUpdate.Material)) {
                    ecb.RemoveComponent<PendingMaterialUpdate>(entity);
                }
            }

            ecb.Playback(EntityManager);
        }

        private bool TryUpdateTrain(Entity entity, Material material) {
            if (!SystemAPI.HasBuffer<TrainCarReference>(entity)) return false;
            var trainCarReferenceBuffer = SystemAPI.GetBuffer<TrainCarReference>(entity);
            using var entities = new NativeList<Entity>(Allocator.Temp);
            for (int i = 0; i < trainCarReferenceBuffer.Length; i++) {
                Entity trainCar = trainCarReferenceBuffer[i];
                if (trainCar == Entity.Null) return false;
                entities.Add(trainCar);
                if (!SystemAPI.HasBuffer<WheelAssemblyReference>(trainCar)) return false;
                var wheelAssemblyReferenceBuffer = SystemAPI.GetBuffer<WheelAssemblyReference>(trainCar);
                for (int j = 0; j < wheelAssemblyReferenceBuffer.Length; j++) {
                    Entity wheelAssembly = wheelAssemblyReferenceBuffer[j];
                    if (wheelAssembly == Entity.Null) return false;
                    entities.Add(wheelAssembly);
                }
            }
            for (int i = 0; i < entities.Length; i++) {
                SetMaterialRecursive(entities[i], material);
            }
            return true;
        }

        private void SetMaterialRecursive(Entity rootEntity, Material material) {
            using var entities = new NativeList<Entity>(Allocator.Temp) {
                rootEntity
            };

            for (int i = 0; i < entities.Length; i++) {
                var entity = entities[i];

                if (SystemAPI.HasComponent<MaterialMeshInfo>(entity) &&
                    EntityManager.HasComponent<RenderMeshArray>(entity)) {

                    var materialMeshInfo = SystemAPI.GetComponent<MaterialMeshInfo>(entity);
                    var renderMeshArray = EntityManager.GetSharedComponentManaged<RenderMeshArray>(entity);

                    var meshes = new Mesh[renderMeshArray.MeshReferences.Length];
                    for (int j = 0; j < meshes.Length; j++) {
                        meshes[j] = renderMeshArray.GetMesh(materialMeshInfo);
                    }

                    var materials = new Material[renderMeshArray.MaterialReferences.Length];
                    for (int j = 0; j < materials.Length; j++) {
                        materials[j] = material;
                    }

                    var newRenderMeshArray = new RenderMeshArray(materials, meshes);
                    EntityManager.SetSharedComponentManaged(entity, newRenderMeshArray);
                }

                if (SystemAPI.HasBuffer<Child>(entity)) {
                    var children = SystemAPI.GetBuffer<Child>(entity);
                    foreach (var child in children) {
                        entities.Add(child.Value);
                    }
                }
            }
        }
    }
}
