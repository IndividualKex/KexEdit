using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;
using Unity.Mathematics;
using static KexEdit.UI.Constants;
using static KexEdit.UI.Timeline.Constants;

namespace KexEdit.UI.Timeline {
    public class TimelineView : VisualElement {
        private Label _tip;
        private Ruler _ruler;
        private DurationHandle _durationHandle;
        private TimelineGap _gap;
        private VisualElement _content;
        private DopeSheetView _dopeSheetView;
        private CurveView _curveView;
        private SelectionBox _selectionBox;

        private Vector2 _prevMousePosition;
        private bool _panning;

        private TimelineData _data;

        public TimelineView() {
            style.position = Position.Relative;
            style.flexGrow = 1f;
            style.alignItems = Align.Stretch;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.backgroundColor = s_DarkBackgroundColor;
            focusable = true;

            _tip = new Label("Select a track section to edit") {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    overflow = Overflow.Hidden,
                    fontSize = 12,
                    color = s_ActiveHoverColor,
                }
            };
            Add(_tip);

            _ruler = new Ruler();
            Add(_ruler);

            _gap = new TimelineGap();
            Add(_gap);

            _content = new VisualElement {
                style = {
                    flexGrow = 1f,
                    alignItems = Align.Stretch,
                    overflow = Overflow.Hidden,
                }
            };
            Add(_content);

            _dopeSheetView = new DopeSheetView();
            _content.Add(_dopeSheetView);

            _curveView = new CurveView();
            _content.Add(_curveView);

            _selectionBox = new SelectionBox();
            Add(_selectionBox);

            _durationHandle = new DurationHandle();
            Add(_durationHandle);

            var tipDisplayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.Active)),
                bindingMode = BindingMode.ToTarget
            };
            tipDisplayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleEnum<DisplayStyle>(value ? DisplayStyle.None : DisplayStyle.Flex));
            _tip.SetBinding("style.display", tipDisplayBinding);
        }

        public void Initialize(TimelineData data) {
            _data = data;

            _ruler.Initialize(data);
            _durationHandle.Initialize(data);
            _gap.Initialize(data);
            _dopeSheetView.Initialize(data);
            _curveView.Initialize(data);

            _content.generateVisualContent += OnDrawContentOverlay;
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void Draw() {
            _data.ViewWidth = resolvedStyle.width;

            _durationHandle.Draw();
            _curveView.Draw();

            _ruler.MarkDirtyRepaint();
            _gap.MarkDirtyRepaint();
            _content.MarkDirtyRepaint();
            _dopeSheetView.MarkDirtyRepaint();
        }

        private void OnDrawContentOverlay(MeshGenerationContext ctx) {
            if (!_data.Active) return;

            var painter = ctx.painter2D;
            Rect rect = _content.contentRect;

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

        private void OnMouseDown(MouseDownEvent evt) {
            if (!_data.Active) return;

            if (evt.button == 2 || (evt.button == 1 && evt.altKey)) {
                _prevMousePosition = evt.localMousePosition;
                _panning = true;
                this.CaptureMouse();
                evt.StopPropagation();
                return;
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_data.Active) return;

            if (_panning) {
                Vector2 delta = evt.localMousePosition - _prevMousePosition;
                delta = Preferences.AdjustPointerDelta(delta);
                _data.Offset -= delta.x;
                _data.ClampOffset();
                _prevMousePosition = evt.localMousePosition;

                var e = this.GetPooled<TimelineOffsetChangeEvent>();
                e.Offset = _data.Offset;
                this.Send(e);

                evt.StopPropagation();
            }
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_data.Active) return;

            if (_panning) {
                _panning = false;
                this.ReleaseMouse();
            }
        }

        private void OnWheel(WheelEvent evt) {
            if (!_data.Active) return;

            if (evt.shiftKey) {
                const float panSpeed = 15f;
                _data.Offset += Preferences.AdjustScroll(evt.delta.y) * panSpeed;
                _data.ClampOffset();

                var e = this.GetPooled<TimelineOffsetChangeEvent>();
                e.Offset = _data.Offset;
                this.Send(e);
            }
            else {
                float zoomMultiplier = 1f - Preferences.AdjustScroll(evt.delta.y) * ZOOM_SPEED;
                float newZoom = _data.Zoom * zoomMultiplier;
                newZoom = Mathf.Clamp(newZoom, MIN_ZOOM, MAX_ZOOM);

                if (Mathf.Abs(_data.Zoom - newZoom) > 1e-6f) {
                    float oldZoom = _data.Zoom;
                    _data.Zoom = newZoom;

                    float mouseTime = _data.PixelToTime(evt.localMousePosition.x);
                    _data.Offset = mouseTime * RESOLUTION * (newZoom - oldZoom) + _data.Offset;
                    _data.ClampOffset();

                    var zoomEvent = this.GetPooled<TimelineZoomChangeEvent>();
                    zoomEvent.Zoom = _data.Zoom;
                    this.Send(zoomEvent);

                    var offsetEvent = this.GetPooled<TimelineOffsetChangeEvent>();
                    offsetEvent.Offset = _data.Offset;
                    this.Send(offsetEvent);
                }
            }

            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt) {
            if (!_data.Active) return;

            if (evt.keyCode == KeyCode.V && !evt.ctrlKey && !evt.commandKey && _data.SelectedKeyframeCount == 1) {
                int propertyIndex = 0;
                foreach (var propertyType in _data.OrderedProperties) {
                    var propertyData = _data.Properties[propertyType];
                    if (!propertyData.Visible) continue;

                    foreach (var keyframe in propertyData.Keyframes) {
                        if (!keyframe.Selected) continue;
                        var keyframeData = new KeyframeData(propertyType, keyframe);

                        float x = _data.TimeToPixel(keyframe.Time);
                        float y = (Constants.ROW_HEIGHT * propertyIndex) + (Constants.ROW_HEIGHT / 2f);
                        var keyframePosition = new Vector2(x, y);

                        var e = this.GetPooled<SetKeyframeValueEvent>();
                        e.Keyframe = keyframeData;
                        e.MousePosition = keyframePosition;
                        this.Send(e);
                        evt.StopPropagation();
                        return;
                    }
                    propertyIndex++;
                }
            }
        }
    }
}
