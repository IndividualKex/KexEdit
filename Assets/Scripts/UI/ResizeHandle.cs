using UnityEngine;
using UnityEngine.UIElements;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public class ResizeHandle : VisualElement {
        private const float MIN_SIZE = 100f;
        private const float SIZE = 10f;

        private ResizeMode _mode;
        private Vector2 _startMouse;
        private float _startSize;
        private bool _dragging;

        public ResizeHandle(ResizeMode mode) {
            _mode = mode;

            style.cursor = _mode == ResizeMode.Vertical ? UIService.SlideVerticalCursor : UIService.SlideHorizontalCursor;
            style.position = Position.Absolute;
            if (_mode == ResizeMode.Vertical) {
                style.left = 0f;
                style.right = 0f;
                style.top = -SIZE / 2f;
                style.height = SIZE;
            }
            else if (_mode == ResizeMode.HorizontalLeft) {
                style.top = 0f;
                style.bottom = 0f;
                style.left = -SIZE / 2f;
                style.width = SIZE;
            }
            else {
                style.top = 0f;
                style.bottom = 0f;
                style.right = -SIZE / 2f;
                style.width = SIZE;
            }
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            var target = parent;
            _startMouse = evt.mousePosition;
            _startSize = _mode == ResizeMode.Vertical ? target.resolvedStyle.height : target.resolvedStyle.width;
            _dragging = true;
            this.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;

            var target = parent;
            var delta = evt.mousePosition - _startMouse;
            float x;
            if (_mode == ResizeMode.Vertical) {
                x = -delta.y;
            }
            else if (_mode == ResizeMode.HorizontalLeft) {
                x = -delta.x;
            }
            else {
                x = delta.x;
            }

            float size = Mathf.Max(MIN_SIZE, _startSize + x);
            if (_mode == ResizeMode.Vertical) {
                target.style.height = size;
            }
            else {
                target.style.width = size;
            }
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_dragging) return;

            _dragging = false;
            this.ReleaseMouse();
            evt.StopPropagation();
        }

        public enum ResizeMode {
            HorizontalLeft,
            HorizontalRight,
            Vertical
        }
    }
}
