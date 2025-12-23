using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Timeline.Constants;

namespace KexEdit.UI.Timeline {
    public class DurationHandle : VisualElement {
        private TimelineData _data;
        private bool _dragging;
        private bool _moved;

        public DurationHandle() {
            style.position = Position.Absolute;
            style.width = DURATION_HANDLE_TOLERANCE * 2;
            style.top = 0;
            style.height = 20f;
            style.backgroundColor = Color.clear;

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.Active)),
                bindingMode = BindingMode.ToTarget
            };
            displayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleEnum<DisplayStyle>(value ? DisplayStyle.Flex : DisplayStyle.None));
            SetBinding("style.display", displayBinding);

            var cursorBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.HasEditableDuration)),
                bindingMode = BindingMode.ToTarget
            };
            cursorBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                (StyleCursor)(value ? UIService.SlideHorizontalCursor : StyleKeyword.None));
            SetBinding("style.cursor", cursorBinding);
        }

        public void Initialize(TimelineData data) {
            _data = data;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        public void Draw() {
            if (!_data.Active) return;

            float x = _data.TimeToPixel(_data.Duration);
            float rulerWidth = parent.contentRect.width;

            if (x < LEFT_PADDING || x > rulerWidth) return;

            style.left = x - DURATION_HANDLE_TOLERANCE;
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (!_data.Active || !_data.HasEditableDuration || evt.button != 0) return;

            _dragging = true;
            _moved = false;
            this.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_data.Active || !_dragging) return;

            Vector2 position = this.LocalToWorld(evt.localMousePosition);
            position = parent.WorldToLocal(position);
            float duration = _data.PixelToTime(position.x);

            if (!_moved && Mathf.Abs(duration - _data.Duration) > 0.001f) {
                _moved = true;
                Undo.Record();
            }

            if (_moved) {
                var e = this.GetPooled<DurationChangeEvent>();
                e.Duration = duration;
                e.Snap = !evt.shiftKey;
                this.Send(e);
            }

            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_data.Active || !_dragging) return;

            _dragging = false;
            this.ReleaseMouse();
            evt.StopPropagation();
        }
    }
}
