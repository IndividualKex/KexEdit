using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    [UpdateAfter(typeof(LoadTrainStyleSettingsSystem))]
    public partial class TrainStyleAssetLoadingSystem : SystemBase {
        private int _trainLayer;

        protected override void OnCreate() {
            _trainLayer = LayerMask.NameToLayer("Train");

            RequireForUpdate<TrainStyleSettings>();
        }

        protected override void OnUpdate() {
            var styleSettings = SystemAPI.ManagedAPI.GetSingleton<TrainStyleSettings>();

            for (int i = 0; i < styleSettings.Styles.Count; i++) {
                var trainStyle = styleSettings.Styles[i];
                if (trainStyle.Loaded || trainStyle.Mesh != Entity.Null) continue;
                trainStyle.Loaded = true;
                string fullPath = System.IO.Path.Combine(
                    UnityEngine.Application.streamingAssetsPath,
                    "TrainStyles",
                    trainStyle.MeshPath
                );

                if (trainStyle.MeshPath.EndsWith(".glb") || trainStyle.MeshPath.EndsWith(".gltf")) {
                    ImportManager.ImportGltfFile(fullPath, EntityManager, _trainLayer, result => {
                        trainStyle.Mesh = result;
                    });
                }
                else if (trainStyle.MeshPath.EndsWith(".obj")) {
                    trainStyle.Mesh = ImportManager.ImportObjFile(fullPath, EntityManager, _trainLayer);
                }
            }
        }
    }
}
