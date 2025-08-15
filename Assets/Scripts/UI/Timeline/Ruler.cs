using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public class Ruler : VisualElement {
        private TimelineData _data;
        private bool _dragging;

        public Ruler() {
            style.height = 20f;
            style.backgroundColor = s_AltBackgroundColor;

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.Active)),
                bindingMode = BindingMode.ToTarget
            };
            displayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleEnum<DisplayStyle>(value ? DisplayStyle.Flex : DisplayStyle.None));
            SetBinding("style.display", displayBinding);
        }

        public void Initialize(TimelineData data) {
            _data = data;

            generateVisualContent += OnDrawContent;
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        private void OnDrawContent(MeshGenerationContext ctx) {
            var painter = ctx.painter2D;
            Rect rect = contentRect;

            if (_data.Active) {
                float minX = _data.TimeToPixel(0);
                if (minX > 0) {
                    painter.fillColor = s_DarkenColor;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(0, 0));
                    painter.LineTo(new Vector2(minX, 0));
                    painter.LineTo(new Vector2(minX, rect.height));
                    painter.LineTo(new Vector2(0, rect.height));
                    painter.Fill();
                }

                float maxX = math.max(0, _data.TimeToPixel(_data.Duration));
                if (maxX < rect.width) {
                    painter.fillColor = s_DarkenColor;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(maxX, 0));
                    painter.LineTo(new Vector2(rect.width, 0));
                    painter.LineTo(new Vector2(rect.width, rect.height));
                    painter.LineTo(new Vector2(maxX, rect.height));
                    painter.Fill();
                }
            }

            TimelineDrawUtils.DrawRulerTicks(ctx, _data, rect);
            TimelineDrawUtils.DrawPlayhead(painter, _data, rect);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0) {
                float time = _data.PixelToTime(evt.localMousePosition.x);
                var e = this.GetPooled<TimeChangeEvent>();
                e.Time = time;
                e.Snap = !evt.shiftKey;
                this.Send(e);
                _dragging = true;
                this.CaptureMouse();
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;
            float time = _data.PixelToTime(evt.localMousePosition.x);
            var e = this.GetPooled<TimeChangeEvent>();
            e.Time = time;
            e.Snap = !evt.shiftKey;
            this.Send(e);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_dragging) return;
            _dragging = false;
            this.ReleaseMouse();
            evt.StopPropagation();
        }
    }
}
