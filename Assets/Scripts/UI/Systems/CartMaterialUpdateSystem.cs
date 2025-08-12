using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class CartMaterialUpdateSystem : SystemBase {
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

            foreach (var rootEntity in entities) {
                var materialUpdate = SystemAPI.ManagedAPI.GetComponent<PendingMaterialUpdate>(rootEntity);
                SetMaterialRecursive(rootEntity, materialUpdate.Material);
                ecb.RemoveComponent<PendingMaterialUpdate>(rootEntity);
            }

            ecb.Playback(EntityManager);
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
