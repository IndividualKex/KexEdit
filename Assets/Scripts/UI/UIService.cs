using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    public class UIService : MonoBehaviour {
        public UIDocument UIDocument;
        public Texture2D CurveButtonTexture;
        public Texture2D TextCursorTexture;
        public Texture2D SlideHorizontalCursorTexture;
        public Texture2D SlideVerticalCursorTexture;
        public Texture2D DropdownTexture;
        public Shader LineGizmoShader;

        public static UIService Instance { get; private set; }
        public static UnityEngine.UIElements.Cursor TextCursor { get; private set; }
        public static UnityEngine.UIElements.Cursor SlideHorizontalCursor { get; private set; }
        public static UnityEngine.UIElements.Cursor SlideVerticalCursor { get; private set; }

        private void Awake() {
            Instance = this;

            var textCursorCursor = new UnityEngine.UIElements.Cursor {
                texture = TextCursorTexture,
                hotspot = new Vector2(16, 16)
            };
            TextCursor = textCursorCursor;

            var slideHorizontalCursor = new UnityEngine.UIElements.Cursor {
                texture = SlideHorizontalCursorTexture,
                hotspot = new Vector2(16, 16)
            };
            SlideHorizontalCursor = slideHorizontalCursor;

            var slideVerticalCursor = new UnityEngine.UIElements.Cursor {
                texture = SlideVerticalCursorTexture,
                hotspot = new Vector2(16, 16)
            };
            SlideVerticalCursor = slideVerticalCursor;
        }
    }
}
