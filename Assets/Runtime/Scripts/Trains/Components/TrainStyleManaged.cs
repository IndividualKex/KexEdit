using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    public class TrainStyleManaged : IComponentData {
        public TrainStyleData Data;

        private HashSet<string> _loading = new();
        private int _layer;

        public Dictionary<string, Entity> Loaded { get; private set; } = new();
        public bool Loading => _loading.Count > 0;

        public TrainStyleManaged() { }

        public TrainStyleManaged(TrainStyleData data, int layer) {
            Data = data;
            _layer = layer;
            Load();
        }

        private void Load() {
            _loading.Clear();
            foreach (var trainCar in Data.TrainCars) {
                _loading.Add(trainCar.MeshPath);
                foreach (var wheelAssembly in trainCar.WheelAssemblies) {
                    _loading.Add(wheelAssembly.MeshPath);
                }
            }

            foreach (var path in _loading) {
                string fullPath = Path.Combine(Application.streamingAssetsPath, path);
                if (path.EndsWith(".glb") || path.EndsWith(".gltf")) {
                    EntityImporter.ImportGltfFile(fullPath, _layer,
                        (result) => OnLoad(path, result), () => OnFailure(path));
                }
                else if (path.EndsWith(".obj")) {
                    EntityImporter.ImportObjFile(fullPath, _layer,
                        (result) => OnLoad(path, result), () => OnFailure(path));
                }
                else {
                    Debug.LogError($"Unsupported file type: {path}");
                    OnFailure(path);
                }
            }
        }

        private void OnLoad(string path, Entity result) {
            _loading.Remove(path);
            Loaded.Add(path, result);
        }

        private void OnFailure(string path) {
            _loading.Remove(path);
        }

        public void Dispose(EntityCommandBuffer ecb) {
            foreach (var entity in Loaded.Values) {
                ecb.DestroyEntity(entity);
            }
            Loaded.Clear();
        }
    }
}
