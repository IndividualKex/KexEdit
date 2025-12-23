using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class UIScaleSystem : SystemBase {
        private UIDocument _uiDocument;
        private float _currentScale = 1f;

        public static UIScaleSystem Instance { get; private set; }

        public float CurrentScale => _currentScale;

        public UIScaleSystem() {
            Instance = this;
        }

        protected override void OnStartRunning() {
            _uiDocument = UIService.Instance.UIDocument;
            _currentScale = Preferences.UIScale;
            ApplyScale();
        }

        protected override void OnUpdate() { }

        public void ZoomIn() {
            SetScale(_currentScale + 0.25f);
        }

        public void ZoomOut() {
            SetScale(_currentScale - 0.25f);
        }

        public void ResetZoom() {
            SetScale(Preferences.GetDefaultUIScale());
        }

        private void SetScale(float scale) {
            _currentScale = Mathf.Clamp(scale, 0.5f, 3f);
            Preferences.UIScale = _currentScale;
            ApplyScale();
        }

        private void ApplyScale() {
            _uiDocument.panelSettings.scale = _currentScale;
        }

        protected override void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }
    }
}
