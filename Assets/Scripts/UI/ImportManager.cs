using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using SFB;
using Unity.Entities;
using Unity.Collections;
using Unity.Rendering;
using ComponentType = Unity.Entities.ComponentType;
using Unity.Entities.Graphics;
using Unity.Transforms;
using Unity.Mathematics;
using System.Threading.Tasks;
using GLTFast;

namespace KexEdit.UI {
    public static class ImportManager {
        public static void ShowImportDialog(VisualElement root, Action<string> onSuccess = null) {
            var extensions = new ExtensionFilter[] {
                new("glTF", "glb", "gltf"),
                new("OBJ", "obj"),
                new("All Files", "*")
            };

            string path = FileManager.ShowOpenFileDialog(extensions);

            if (string.IsNullOrEmpty(path)) {
                Debug.Log("Import canceled or no file selected.");
                return;
            }

            onSuccess?.Invoke(path);
        }

        public static void ShowImportDialog(VisualElement root, ExtensionFilter[] extensions, Action<string> onSuccess = null) {
            string path = FileManager.ShowOpenFileDialog(extensions);

            if (string.IsNullOrEmpty(path)) {
                Debug.Log("Import canceled or no file selected.");
                return;
            }

            onSuccess?.Invoke(path);
        }

        public static async void ImportGltfFile(string path, EntityManager entityManager, int layer, Action<Entity> onSuccess = null) {
            var gltf = new GltfImport();
            bool success = await gltf.Load(path);

            if (!success) {
                Debug.LogError("Failed to load glTF.");
                return;
            }

            string name = $"Imported glTF: {Path.GetFileNameWithoutExtension(path)}";
            var entity = entityManager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
            entityManager.SetName(entity, name);
            entityManager.SetComponentData(entity, LocalTransform.Identity);
            entityManager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });

            var settings = new InstantiationSettings {
                SceneObjectCreation = SceneObjectCreation.Never
            };
            var instantiator = new EntityInstantiator(gltf, entity, null, settings);
            success = await gltf.InstantiateMainSceneAsync(instantiator);

            if (!success) {
                Debug.LogError("Failed to instantiate glTF.");
                return;
            }

            await Task.Yield();

            InitializeEntityHierarchy(entityManager, entity, layer);
            onSuccess?.Invoke(entity);
        }

        public static Entity ImportObjFile(string path, EntityManager entityManager, int layer) {
            try {
                Mesh mesh = ObjLoader.LoadMesh(path);
                Material material = Resources.Load<Material>("Default-PBR");

                if (mesh == null) {
                    Debug.LogError("Failed to parse OBJ file.");
                    return Entity.Null;
                }

                string name = $"Imported OBJ: {Path.GetFileNameWithoutExtension(path)}";
                var entity = entityManager.CreateEntity();
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                ecb.SetName(entity, name);
                AddRenderingComponents(entity, ecb, mesh, Resources.Load<Material>("Default-PBR"), layer);
                ecb.Playback(entityManager);

                return entity;
            }
            catch (Exception e) {
                Debug.LogError($"Error importing OBJ file: {e.Message}");
                return Entity.Null;
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
            linkedEntityGroup.Add(rootEntity);
            foreach (var entity in entities) {
                linkedEntityGroup.Add(entity);
            }

            ecb.SetComponent(rootEntity, LocalTransform.FromPosition(new float3(0f, -999f, 0f)));

            ecb.Playback(entityManager);
        }
    }
}
