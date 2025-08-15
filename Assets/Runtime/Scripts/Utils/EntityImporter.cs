using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using ComponentType = Unity.Entities.ComponentType;

namespace KexEdit {
    public static class EntityImporter {
        public static async void ImportGltfFile(string path, int layer, Action<Entity> onSuccess = null, Action onFailure = null) {
            if (!File.Exists(path)) {
                Debug.LogError($"GLTF file not found: {path}");
                return;
            }

            try {
                var gltf = new GltfImport();
                bool success = await gltf.Load(path);

                if (!success) {
                    Debug.LogError($"Failed to load glTF: {path}");
                    return;
                }

                await Awaitable.MainThreadAsync();

                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) {
                    return;
                }

                var entityManager = world.EntityManager;
                string name = $"Imported glTF: {Path.GetFileNameWithoutExtension(path)}";
                var entity = entityManager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
                entityManager.SetName(entity, name);
                var transform = LocalTransform.FromPosition(new float3(0f, -999f, 0f));
                entityManager.SetComponentData(entity, transform);
                entityManager.SetComponentData(entity, new LocalToWorld { Value = transform.ToMatrix() });

                var settings = new InstantiationSettings {
                    SceneObjectCreation = SceneObjectCreation.Never
                };
                var instantiator = new EntityInstantiator(gltf, entity, null, settings);

                success = await gltf.InstantiateMainSceneAsync(instantiator);

                await Task.Yield();
                await Awaitable.MainThreadAsync();

                if (world == null || !world.IsCreated) {
                    return;
                }

                entityManager = world.EntityManager;
                if (!success) {
                    Debug.LogError($"Failed to instantiate glTF: {path}");
                    if (entityManager.Exists(entity)) {
                        entityManager.DestroyEntity(entity);
                    }
                    return;
                }

                if (entityManager.Exists(entity)) {
                    InitializeEntityHierarchy(entityManager, entity, layer);
                    onSuccess?.Invoke(entity);
                }
                else {
                    Debug.LogError($"ImportGltfFile: Entity {entity} does not exist");
                }
            }
            catch (Exception ex) {
                Debug.LogError($"Error importing GLTF {path}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void ImportObjFile(string path, int layer, Action<Entity> onSuccess = null, Action onFailure = null) {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) {
                return;
            }
            var entityManager = world.EntityManager;

            if (!File.Exists(path)) {
                Debug.LogError($"OBJ file not found: {path}");
                onFailure?.Invoke();
                return;
            }

            try {
                Mesh mesh = ObjImporter.LoadMesh(path);
                Material material = Resources.Load<Material>("Default-PBR");

                if (mesh == null) {
                    Debug.LogError($"Failed to parse OBJ file: {path}");
                    onFailure?.Invoke();
                    return;
                }

                string name = $"Imported OBJ: {Path.GetFileNameWithoutExtension(path)}";
                var entity = entityManager.CreateEntity();
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                ecb.SetName(entity, name);
                AddRenderingComponents(entity, ecb, mesh, Resources.Load<Material>("Default-PBR"), layer);
                ecb.Playback(entityManager);

                onSuccess?.Invoke(entity);
            }
            catch (Exception e) {
                Debug.LogError($"Error importing OBJ file: {e.Message}");
                onFailure?.Invoke();
            }
        }

        private static void AddRenderingComponents(
            Entity entity,
            EntityCommandBuffer ecb,
            Mesh mesh,
            Material material,
            int layer
        ) {
            var renderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh });
            var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

            var componentTypes = new FixedList128Bytes<ComponentType>() {
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<WorldRenderBounds>(),
                ComponentType.ReadWrite<RenderFilterSettings>(),
                ComponentType.ReadWrite<MaterialMeshInfo>(),
                ComponentType.ChunkComponent<ChunkWorldRenderBounds>(),
                ComponentType.ChunkComponent<EntitiesGraphicsChunkInfo>(),
                ComponentType.ReadWrite<WorldToLocal_Tag>(),
                ComponentType.ReadWrite<RenderBounds>(),
                ComponentType.ReadWrite<PerInstanceCullingTag>(),
                ComponentType.ReadWrite<RenderMeshArray>(),
                ComponentType.ReadWrite<LocalToWorld>()
            };

            ecb.AddComponent(entity, new ComponentTypeSet(componentTypes));

            var transform = LocalTransform.FromPosition(new float3(0f, -999f, 0f));
            ecb.SetComponent(entity, transform);

            var renderFilterSettings = RenderFilterSettings.Default;
            renderFilterSettings.Layer = layer;
            ecb.SetSharedComponent(entity, renderFilterSettings);

            ecb.SetSharedComponentManaged(entity, renderMeshArray);
            ecb.SetComponent(entity, materialMeshInfo);
            ecb.SetComponent(entity, new RenderBounds { Value = mesh.bounds.ToAABB() });
        }

        private static void InitializeEntityHierarchy(EntityManager entityManager, Entity rootEntity, int layer) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var entities = new NativeList<Entity>(Allocator.Temp) {
                rootEntity
            };

            for (int i = 0; i < entities.Length; i++) {
                var entity = entities[i];

                if (entityManager.HasComponent<Disabled>(entity)) {
                    ecb.RemoveComponent<Disabled>(entity);
                }

                if (entityManager.HasComponent<RenderFilterSettings>(entity)) {
                    var renderFilterSettings = entityManager.GetSharedComponent<RenderFilterSettings>(entity);
                    renderFilterSettings.Layer = layer;
                    ecb.SetSharedComponent(entity, renderFilterSettings);
                }

                if (entityManager.HasBuffer<Child>(entity)) {
                    var children = entityManager.GetBuffer<Child>(entity);
                    foreach (var child in children) {
                        entities.Add(child.Value);
                    }
                }
            }

            var linkedEntityGroup = entityManager.AddBuffer<LinkedEntityGroup>(rootEntity);
            foreach (var entity in entities) {
                linkedEntityGroup.Add(entity);
            }

            ecb.SetComponent(rootEntity, LocalTransform.FromPosition(new float3(0f, -999f, 0f)));

            ecb.Playback(entityManager);
        }
    }
}
