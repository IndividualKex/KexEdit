using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UxmlElement]
    public partial class GameView : VisualElement {
        private RenderTexture _texture;

        public GameView() {
            style.flexGrow = 1;

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent e) {
            if (_texture != null && _texture.IsCreated()) {
                _texture.Release();
            }

            int width = (int)resolvedStyle.width;
            int height = (int)resolvedStyle.height;
            if (width == 0 || height == 0) return;

            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) {
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                antiAliasing = 4
            };
            _texture.Create();
            Camera.main.targetTexture = _texture;
            style.backgroundImage = Background.FromRenderTexture(_texture);
        }
    }
}
