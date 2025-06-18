using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    public class UIService : MonoBehaviour {
        public UIDocument UIDocument;
        public Texture2D CurveButtonTexture;
        public Shader LineGizmoShader;

        public static UIService Instance { get; private set; }
        public static UnityEngine.UIElements.Cursor TextCursor { get; private set; }
        public static UnityEngine.UIElements.Cursor SlideHorizontalCursor { get; private set; }
        public static UnityEngine.UIElements.Cursor SlideVerticalCursor { get; private set; }

        private void Awake() {
            Instance = this;

            var textCursor = Resources.Load<Texture2D>("TextCursor");
            var textCursorCursor = new UnityEngine.UIElements.Cursor {
                texture = textCursor,
                hotspot = new Vector2(16, 16)
            };
            TextCursor = textCursorCursor;

            var slideHorizontalArrow = Resources.Load<Texture2D>("SlideHorizontal");
            var slideHorizontalCursor = new UnityEngine.UIElements.Cursor {
                texture = slideHorizontalArrow,
                hotspot = new Vector2(16, 16)
            };
            SlideHorizontalCursor = slideHorizontalCursor;

            var slideVerticalArrow = Resources.Load<Texture2D>("SlideVertical");
            var slideVerticalCursor = new UnityEngine.UIElements.Cursor {
                texture = slideVerticalArrow,
                hotspot = new Vector2(16, 16)
            };
            SlideVerticalCursor = slideVerticalCursor;
        }
    }
}
