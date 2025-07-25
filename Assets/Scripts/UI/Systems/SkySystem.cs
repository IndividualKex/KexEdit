using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class SkySystem : SystemBase {
        private Material _proceduralSkyMaterial;
        private Camera _mainCamera;
        private SkyType _currentSkyType = SkyType.Solid;

        protected override void OnCreate() {
            _mainCamera = Camera.main;
        }

        protected override void OnStartRunning() {
            CreateProceduralSkyMaterial();
            ApplySkyType(Preferences.SkyType);
        }

        protected override void OnUpdate() {
            if (_currentSkyType != Preferences.SkyType) {
                ApplySkyType(Preferences.SkyType);
            }
        }

        private void CreateProceduralSkyMaterial() {
            _proceduralSkyMaterial = new Material(Shader.Find("Skybox/Procedural"));
            
            _proceduralSkyMaterial.SetFloat("_SunSize", 0.04f);
            _proceduralSkyMaterial.SetFloat("_SunSizeConvergence", 5f);
            _proceduralSkyMaterial.SetFloat("_AtmosphereThickness", 1f);
            _proceduralSkyMaterial.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f, 1f));
            _proceduralSkyMaterial.SetColor("_GroundColor", new Color(0.369f, 0.349f, 0.341f, 1f));
            _proceduralSkyMaterial.SetFloat("_Exposure", 1.3f);
        }

        private void ApplySkyType(SkyType skyType) {
            _currentSkyType = skyType;

            if (_mainCamera == null) {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            switch (skyType) {
                case SkyType.Solid:
                    RenderSettings.skybox = null;
                    _mainCamera.clearFlags = CameraClearFlags.SolidColor;
                    _mainCamera.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
                    break;

                case SkyType.Procedural:
                    if (_proceduralSkyMaterial != null) {
                        RenderSettings.skybox = _proceduralSkyMaterial;
                        _mainCamera.clearFlags = CameraClearFlags.Skybox;
                    }
                    break;
            }

            DynamicGI.UpdateEnvironment();
        }

        protected override void OnDestroy() {
            if (_proceduralSkyMaterial != null) {
                if (Application.isPlaying) {
                    Object.Destroy(_proceduralSkyMaterial);
                } else {
                    Object.DestroyImmediate(_proceduralSkyMaterial);
                }
            }
        }
    }
}
