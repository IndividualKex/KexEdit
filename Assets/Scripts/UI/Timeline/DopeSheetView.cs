using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;
using static KexEdit.UI.Timeline.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI.Timeline {
    public class DopeSheetView : VisualElement {
        private SelectionBox _selectionBox;

        private List<KeyframeData> _selected = new();
        private Dictionary<uint, float> _startTimes = new();
        private Vector2 _startMousePosition;
        private Vector2 _lastMouseDownPosition;
        private KeyframeData _draggedKeyframe;
        private bool _dragging;
        private bool _boxSelecting;
        private bool _moved;

        private TimelineData _data;

        public DopeSheetView() {
            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;

            _selectionBox = new SelectionBox();
            Add(_selectionBox);

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.ViewMode)),
                bindingMode = BindingMode.ToTarget
            };
            displayBinding.sourceToUiConverters.AddConverter((ref TimelineViewMode value) =>
                new StyleEnum<DisplayStyle>(value == TimelineViewMode.DopeSheet ? DisplayStyle.Flex : DisplayStyle.None));
            SetBinding("style.display", displayBinding);
        }

        public void Initialize(TimelineData data) {
            _data = data;

            generateVisualContent += OnDrawContent;
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<ClickEvent>(OnClick);
        }

        private void OnDrawContent(MeshGenerationContext ctx) {
            if (!_data.Active) return;

            var painter = ctx.painter2D;

            TimelineDrawUtils.DrawPlayhead(painter, _data, contentRect);
            DrawGrid(painter, _data, contentRect);
            DrawKeyframes(painter, _data, contentRect);
        }

        private void DrawGrid(Painter2D painter, TimelineData data, Rect rect) {
            TimelineDrawUtils.DrawVerticalGrid(painter, data, rect);
            int visiblePropertyCount = 0;
            foreach (var (type, propertyData) in data.Properties) {
                if (!propertyData.Visible) continue;
                visiblePropertyCount++;
            }
            TimelineDrawUtils.DrawHorizontalPropertyLines(painter, rect, visiblePropertyCount);
        }

        private void DrawKeyframes(Painter2D painter, TimelineData data, Rect rect) {
            const float size = KEYFRAME_SIZE;
            const float halfSize = size / 2f;

            int i = 0;
            foreach (var type in data.OrderedProperties) {
                var propertyData = data.Properties[type];
                if (!propertyData.Visible) continue;

                float top = ROW_HEIGHT * i++;
                float y = top + ROW_HEIGHT / 2f;
                foreach (var keyframe in propertyData.Keyframes) {
                    float x = data.TimeToPixel(keyframe.Time);

                    Color keyframeColor = keyframe.Selected ? s_BlueOutline : s_TextColor;
                    painter.fillColor = keyframeColor;
                    painter.BeginPath();

                    switch (keyframe.OutInterpolation) {
                        case InterpolationType.Constant:
                            painter.MoveTo(new Vector2(x - halfSize, y - halfSize));
                            painter.LineTo(new Vector2(x + halfSize, y - halfSize));
                            painter.LineTo(new Vector2(x + halfSize, y + halfSize));
                            painter.LineTo(new Vector2(x - halfSize, y + halfSize));
                            break;
                        case InterpolationType.Linear:
                            painter.MoveTo(new Vector2(x, y - halfSize));
                            painter.LineTo(new Vector2(x + halfSize, y));
                            painter.LineTo(new Vector2(x, y + halfSize));
                            painter.LineTo(new Vector2(x - halfSize, y));
                            break;
                        case InterpolationType.Bezier:
                        default:
                            painter.Arc(new Vector2(x, y), halfSize, 0, 360);
                            break;
                    }

                    painter.Fill();

                    if (keyframe.IsTimeLocked || keyframe.IsValueLocked) {
                        painter.DrawKeyframeLockBorder(x, y, halfSize, keyframe.IsTimeLocked, keyframe.IsValueLocked, keyframeColor);
                    }
                }
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (!_data.Active) return;

            Focus();
            _lastMouseDownPosition = evt.localMousePosition;

            bool hasKeyframe = TryGetKeyframeAtPosition(evt.localMousePosition, out KeyframeData keyframe);
            if (evt.button == 0) {
                if (hasKeyframe) {
                    _draggedKeyframe = keyframe;
                    _startMousePosition = evt.localMousePosition;
                    _dragging = true;
                    _moved = false;
                }
                else {
                    var e = this.GetPooled<ViewClickEvent>();
                    e.MousePosition = evt.localMousePosition;
                    e.ShiftKey = evt.shiftKey;
                    this.Send(e);
                    _selectionBox.Begin(evt.localMousePosition);
                    _boxSelecting = true;
                }
                this.CaptureMouse();
                evt.StopPropagation();
            }
            else if (evt.button == 1 && !evt.altKey) {
                if (hasKeyframe && !keyframe.Value.Selected) {
                    var selectEvent = this.GetPooled<KeyframeClickEvent>();
                    selectEvent.Keyframe = keyframe;
                    selectEvent.ShiftKey = false;
                    this.Send(selectEvent);
                }
                
                var e = this.GetPooled<ViewRightClickEvent>();
                e.MousePosition = evt.localMousePosition;
                this.Send(e);
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_data.Active) return;

            if (_dragging) {
                Vector2 delta = evt.localMousePosition - _startMousePosition;
                float timeDelta = delta.x / (_data.Zoom * RESOLUTION);

                if (!_moved && Mathf.Abs(timeDelta) > 1e-3f) {
                    _moved = true;
                    if (!_draggedKeyframe.Value.Selected) {
                        var selectEvent = this.GetPooled<KeyframeClickEvent>();
                        selectEvent.Keyframe = _draggedKeyframe;
                        selectEvent.ShiftKey = false;
                        this.Send(selectEvent);
                    }
                    StoreKeyframes();
                    Undo.Record();
                }

                if (_moved) {
                    var e = this.GetPooled<DragKeyframesEvent>();
                    e.StartTimes = _startTimes;
                    e.StartValues = null;
                    e.TimeDelta = timeDelta;
                    e.ValueDelta = 0;
                    e.Bounds = _data.ValueBounds;
                    e.ContentHeight = contentRect.height;
                    e.ShiftKey = evt.shiftKey;
                    this.Send(e);
                }

                evt.StopPropagation();
            }

            if (_boxSelecting) {
                _selectionBox.Draw(evt.localMousePosition);
            }
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_data.Active) return;

            if (_dragging) {
                _dragging = false;
                this.ReleaseMouse();
            }

            if (_boxSelecting) {
                var rect = _selectionBox.Close();
                BoxSelect(rect);
                _boxSelecting = false;
                this.ReleaseMouse();
            }
        }

        private void OnClick(ClickEvent evt) {
            if (!_data.Active) return;

            if (_moved) {
                _moved = false;
                return;
            }

            bool hasKeyframe = TryGetKeyframeAtPosition(_lastMouseDownPosition, out KeyframeData keyframe);
            if (hasKeyframe) {
                if (evt.clickCount == 2 && !evt.shiftKey) {
                    var e = this.GetPooled<KeyframeDoubleClickEvent>();
                    e.Keyframe = keyframe;
                    e.MousePosition = _lastMouseDownPosition;
                    this.Send(e);
                    evt.StopPropagation();
                }
                else if (evt.clickCount == 1) {
                    var e = this.GetPooled<KeyframeClickEvent>();
                    e.Keyframe = keyframe;
                    e.ShiftKey = evt.shiftKey;
                    this.Send(e);
                    evt.StopPropagation();
                }
            }
        }

        private void ClickKeyframe(KeyframeData keyframe, bool shiftKey) {
            if (shiftKey && keyframe.Value.Selected) return;

            var e = this.GetPooled<KeyframeClickEvent>();
            e.Keyframe = keyframe;
            e.ShiftKey = shiftKey;
            this.Send(e);
        }

        private void StoreKeyframes() {
            _startTimes.Clear();
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;
                foreach (var keyframe in propertyData.Keyframes) {
                    _startTimes[keyframe.Id] = keyframe.Time;
                }
            }
        }

        private bool TryGetKeyframeAtPosition(Vector2 pos, out KeyframeData result) {
            result = default;

            const float tolerance = KEYFRAME_SIZE * 1.5f;

            int i = 0;
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible) continue;

                float top = ROW_HEIGHT * i++;
                float bottom = top + ROW_HEIGHT;
                float y = (top + bottom) / 2f;

                if (pos.y < top || pos.y > bottom) continue;

                foreach (var keyframe in propertyData.Keyframes) {
                    float x = _data.TimeToPixel(keyframe.Time);
                    Vector2 keyframePos = new(x, y);
                    if (Vector2.Distance(pos, keyframePos) < tolerance) {
                        result = new KeyframeData(type, keyframe);
                        return true;
                    }
                }
            }

            return false;
        }

        private void BoxSelect(Rect rect) {
            const float size = KEYFRAME_SIZE;
            const float halfSize = size / 2f;

            int i = 0;
            _selected.Clear();
            foreach (var type in _data.OrderedProperties) {
                var propertyData = _data.Properties[type];
                if (!propertyData.Visible) continue;

                float top = ROW_HEIGHT * i++;
                float bottom = top + ROW_HEIGHT;
                float y = (top + bottom) / 2f;

                if (rect.y + rect.height <= top || rect.y >= bottom) continue;

                foreach (var keyframe in propertyData.Keyframes) {
                    float x = _data.TimeToPixel(keyframe.Time);

                    Rect keyframeBounds = new(
                        x - halfSize,
                        y - halfSize,
                        size,
                        size
                    );

                    if (rect.Overlaps(keyframeBounds)) {
                        _selected.Add(new KeyframeData(type, keyframe));
                    }
                }
            }

            var e = this.GetPooled<SelectKeyframesEvent>();
            e.Keyframes = _selected;
            this.Send(e);
        }
    }
}
