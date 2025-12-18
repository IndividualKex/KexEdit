using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public class SelectionBox : VisualElement {
        private Vector2 _start;
        private Vector2 _end;

        public SelectionBox() {
            style.position = Position.Absolute;
            style.borderTopWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderTopColor = s_BlueOutlineTransparent;
            style.borderRightColor = s_BlueOutlineTransparent;
            style.borderBottomColor = s_BlueOutlineTransparent;
            style.borderLeftColor = s_BlueOutlineTransparent;
            style.display = DisplayStyle.None;
        }

        public void Begin(Vector2 start) {
            _start = start;
            Draw(start);
            style.display = DisplayStyle.Flex;
        }

        public void Draw(Vector2 end) {
            _end = end;
            style.left = Mathf.Min(_start.x, end.x);
            style.top = Mathf.Min(_start.y, end.y);
            style.width = Mathf.Abs(end.x - _start.x);
            style.height = Mathf.Abs(end.y - _start.y);
        }

        public Rect Close() {
            style.display = DisplayStyle.None;
            Vector2 position = new(Mathf.Min(_start.x, _end.x), Mathf.Min(_start.y, _end.y));
            Vector2 size = new(Mathf.Abs(_end.x - _start.x), Mathf.Abs(_end.y - _start.y));
            return new Rect(position, size);
        }
    }
}
