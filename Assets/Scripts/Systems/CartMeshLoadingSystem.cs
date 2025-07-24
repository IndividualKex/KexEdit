using Unity.Entities;
using KexEdit.UI;
using UnityEngine;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(LoadCartStyleSettingsSystem))]
    public partial class CartMeshLoadingSystem : SystemBase {
        private int _cartLayer;

        protected override void OnCreate() {
            _cartLayer = LayerMask.NameToLayer("Cart");

            RequireForUpdate<CartStyleSettings>();
        }

        protected override void OnUpdate() {
            var styleSettings = SystemAPI.ManagedAPI.GetSingleton<CartStyleSettings>();

            for (int i = 0; i < styleSettings.Styles.Count; i++) {
                var cartStyle = styleSettings.Styles[i];
                if (cartStyle.Loaded || cartStyle.Mesh != null) continue;
                cartStyle.Loaded = true;
                string fullPath = System.IO.Path.Combine(
                    UnityEngine.Application.streamingAssetsPath,
                    "CartStyles",
                    cartStyle.MeshPath
                );

                if (cartStyle.MeshPath.EndsWith(".glb") || cartStyle.MeshPath.EndsWith(".gltf")) {
                    ImportManager.ImportGltfFileAsync(fullPath, gameObject => {
                        gameObject.name = cartStyle.MeshPath;
                        gameObject.SetActive(false);
                        SetLayerRecursive(gameObject, _cartLayer);
                        cartStyle.Mesh = gameObject;
                    });
                }
                else if (cartStyle.MeshPath.EndsWith(".obj")) {
                    ImportManager.ImportObjFile(fullPath, gameObject => {
                        gameObject.name = cartStyle.MeshPath;
                        gameObject.SetActive(false);
                        SetLayerRecursive(gameObject, _cartLayer);
                        cartStyle.Mesh = gameObject;
                    });
                }
            }
        }

        private void SetLayerRecursive(GameObject gameObject, int layer) {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform) {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}
