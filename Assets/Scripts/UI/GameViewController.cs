using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    public class GameViewController : MonoBehaviour {
        public Camera Camera;
        public UIDocument UIDocument;

        private VisualElement _gameView;
        private RenderTexture _texture;

        private void Start() {
            var root = UIDocument.rootVisualElement;
            _gameView = root.Q<VisualElement>("GameView");
            _gameView.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent e) {
            int width = (int)_gameView.resolvedStyle.width;
            int height = (int)_gameView.resolvedStyle.height;
            if (_texture != null && _texture.IsCreated()) {
                _texture.Release();
            }
            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) {
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                antiAliasing = 4
            };
            _texture.Create();
            Camera.targetTexture = _texture;
            _gameView.style.backgroundImage = Background.FromRenderTexture(_texture);
        }
    }
}
