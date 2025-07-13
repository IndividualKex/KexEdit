using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Timeline.Constants;

namespace KexEdit.UI.Timeline {
    public class CurveView : VisualElement {
        private SelectionBox _selectionBox;

        private List<KeyframeData> _selected = new();
        private Dictionary<uint, float> _startTimes = new();
        private Dictionary<uint, float> _startValues = new();
        private Vector2 _startMousePosition;
        private Vector2 _mousePosition;
        private Vector2 _lastMouseDownPosition;
        private ValueBounds _startBounds;
        private ValueBounds _dragBounds;
        private KeyframeData _draggedKeyframe;
        private KeyframeData _bezierKeyframe;
        private float _startBezierTime;
        private float _startBezierValue;
        private float _pan;
        private bool _draggingKeyframe;
        private bool _draggingBezierHandle;
        private bool _isBezierOutHandle;
        private bool _boxSelecting;
        private bool _moved;
        private bool _shiftKey;

        private TimelineData _data;

        public CurveView() {
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
                new StyleEnum<DisplayStyle>(value == TimelineViewMode.Curve ? DisplayStyle.Flex : DisplayStyle.None));
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

        public void Draw() {
            if ((_draggingKeyframe || _draggingBezierHandle) && _moved) {
                UpdatePanning();
            }

            MarkDirtyRepaint();
        }

        private void OnDrawContent(MeshGenerationContext ctx) {
            if (!_data.Active) return;

            var painter = ctx.painter2D;

            var bounds = _draggingKeyframe || _draggingBezierHandle ? _dragBounds : _data.ValueBounds;

            TimelineDrawUtils.DrawVerticalGrid(painter, _data, contentRect);
            TimelineDrawUtils.DrawValueLegend(ctx, _data, bounds, contentRect);
            TimelineDrawUtils.DrawPlayhead(painter, _data, contentRect);

            foreach (var (type, propertyData) in _data.Properties) {
                if (propertyData.DrawReadOnly) {
                    painter.DrawCurvesReadOnly(_data, bounds, propertyData, contentRect);
                    continue;
                }

                if (!propertyData.Visible || propertyData.Hidden) continue;

                painter.DrawCurves(_data, bounds, propertyData, contentRect);
                painter.DrawKeyframes(_data, bounds, propertyData, contentRect);
                painter.DrawBezierHandles(_data, bounds, propertyData, contentRect);
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (!_data.Active) return;

            Focus();
            _lastMouseDownPosition = evt.localMousePosition;

            bool hasKeyframe = TryGetKeyframeAtPosition(
                _data.ValueBounds,
                evt.localMousePosition,
                out KeyframeData keyframe
            );
            bool hasBezierHandle = TryGetBezierHandleAtPosition(
                _data.ValueBounds,
                evt.localMousePosition,
                out KeyframeData bezierKeyframe,
                out bool isBezierOutHandle
            );

            if (evt.button == 0) {
                if (hasKeyframe) {
                    _draggedKeyframe = keyframe;
                    _startBounds = _data.ValueBounds;
                    _dragBounds = _data.ValueBounds;
                    _startMousePosition = evt.localMousePosition;
                    _pan = 0f;
                    _draggingKeyframe = true;
                    _moved = false;
                }
                else if (hasBezierHandle) {
                    _bezierKeyframe = bezierKeyframe;
                    _isBezierOutHandle = isBezierOutHandle;
                    StoreBezierHandlePosition();
                    _startBounds = _data.ValueBounds;
                    _dragBounds = _data.ValueBounds;
                    _startMousePosition = evt.localMousePosition;
                    _pan = 0f;
                    _draggingBezierHandle = true;
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

            _mousePosition = evt.localMousePosition;
            _shiftKey = evt.shiftKey;

            if (_draggingKeyframe || _draggingBezierHandle) {
                Vector2 delta = _mousePosition - _startMousePosition;
                ComputeDeltas(delta, out float timeDelta, out float valueDelta);

                if (!_moved && (Mathf.Abs(timeDelta) > 1e-3f || Mathf.Abs(valueDelta) > 1e-3f)) {
                    _moved = true;
                    if (_draggingKeyframe && !_draggedKeyframe.Value.Selected) {
                        var selectEvent = this.GetPooled<KeyframeClickEvent>();
                        selectEvent.Keyframe = _draggedKeyframe;
                        selectEvent.ShiftKey = false;
                        this.Send(selectEvent);
                    }
                    StoreKeyframes();
                    Undo.Record();
                }

                if (_moved) {
                    if (_draggingKeyframe) {
                        UpdateKeyframes(timeDelta, valueDelta);
                    }
                    else if (_draggingBezierHandle) {
                        UpdateBezierHandle(timeDelta, valueDelta);
                    }
                }

                evt.StopPropagation();
            }

            if (_boxSelecting) {
                _selectionBox.Draw(evt.localMousePosition);
            }
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_data.Active) return;

            if (_draggingKeyframe) {
                _draggingKeyframe = false;
                this.ReleaseMouse();
            }

            if (_draggingBezierHandle) {
                _draggingBezierHandle = false;
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

            bool hasKeyframe = TryGetKeyframeAtPosition(_data.ValueBounds, _lastMouseDownPosition, out KeyframeData keyframe);
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
            if (shiftKey && keyframe.Value.Selected) {
                return;
            }

            var e = this.GetPooled<KeyframeClickEvent>();
            e.Keyframe = keyframe;
            e.ShiftKey = shiftKey;
            this.Send(e);
        }

        private void ComputeDeltas(Vector2 delta, out float timeDelta, out float valueDelta) {
            timeDelta = delta.x / (_data.Zoom * RESOLUTION);

            valueDelta = 0f;

            float startY = _startMousePosition.y;
            float y = _startMousePosition.y + delta.y;

            float startValue = _startBounds.PixelToValue(startY, contentRect.height);
            float value = _startBounds.PixelToValue(y, contentRect.height);
            valueDelta = value - startValue + _pan;

            if (_shiftKey) {
                float normalizedTimeDelta = 0f;
                float normalizedValueDelta = 0f;

                float contentWidth = contentRect.width - LEFT_PADDING;
                float visibleTimeRange = contentWidth > 0f ? contentWidth / (_data.Zoom * RESOLUTION) : 1f;
                normalizedTimeDelta = Mathf.Abs(timeDelta) / visibleTimeRange;

                if (_startBounds.Range > 0) {
                    float ratio = contentRect.height / contentRect.width;
                    normalizedValueDelta = Mathf.Abs(valueDelta) / _startBounds.Range * ratio;
                }

                if (normalizedTimeDelta < normalizedValueDelta) {
                    timeDelta = 0f;
                }
                else {
                    valueDelta = 0f;
                }
            }
        }

        private void StoreKeyframes() {
            _startTimes.Clear();
            _startValues.Clear();
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible || propertyData.Hidden) continue;
                foreach (var keyframe in propertyData.Keyframes) {
                    _startTimes[keyframe.Id] = keyframe.Time;
                    _startValues[keyframe.Id] = keyframe.Value;
                }
            }
        }

        private void StoreBezierHandlePosition() {
            var keyframes = _data.Properties[_bezierKeyframe.Type].Keyframes;
            for (int i = 0; i < keyframes.Count; i++) {
                var keyframe = keyframes[i];
                if (keyframe.Id != _bezierKeyframe.Value.Id) continue;

                if (_isBezierOutHandle && i < keyframes.Count - 1) {
                    var next = keyframes[i + 1];
                    float dt = next.Time - keyframe.Time;
                    _startBezierTime = keyframe.Time + (dt * keyframe.OutWeight);
                    _startBezierValue = keyframe.Value + (keyframe.OutTangent * dt * keyframe.OutWeight);
                }
                else if (!_isBezierOutHandle && i > 0) {
                    var prev = keyframes[i - 1];
                    float dt = keyframe.Time - prev.Time;
                    _startBezierTime = keyframe.Time - (dt * keyframe.InWeight);
                    _startBezierValue = keyframe.Value - (keyframe.InTangent * dt * keyframe.InWeight);
                }
                break;
            }
        }

        private void UpdatePanning() {
            float height = contentRect.height;
            float y = _mousePosition.y;
            float value = _dragBounds.PixelToValue(y, height);

            ValueBounds visualBounds = _dragBounds.ComputeVisualBounds(height);

            float panAmount = 0f;

            if (value < visualBounds.Min) {
                panAmount = (value - visualBounds.Min) * PAN_SPEED;
            }
            else if (value > visualBounds.Max) {
                panAmount = (value - visualBounds.Max) * PAN_SPEED;
            }

            if (Mathf.Abs(panAmount) > 0.001f) {
                _dragBounds.Pan(panAmount);
                _pan += panAmount;

                Vector2 delta = _mousePosition - _startMousePosition;
                ComputeDeltas(delta, out float timeDelta, out float valueDelta);
                if (_draggingKeyframe) {
                    UpdateKeyframes(timeDelta, valueDelta);
                }
                else if (_draggingBezierHandle) {
                    UpdateBezierHandle(timeDelta, valueDelta);
                }
            }
        }

        private void UpdateKeyframes(float timeDelta, float valueDelta) {
            var e = this.GetPooled<DragKeyframesEvent>();
            e.StartTimes = _startTimes;
            e.StartValues = _startValues;
            e.TimeDelta = timeDelta;
            e.ValueDelta = valueDelta;
            e.Bounds = _dragBounds;
            e.ContentHeight = contentRect.height;
            this.Send(e);
        }

        private void UpdateBezierHandle(float timeDelta, float valueDelta) {
            var e = this.GetPooled<DragBezierHandleEvent>();
            e.Keyframe = _bezierKeyframe;
            e.IsOutHandle = _isBezierOutHandle;
            e.StartTime = _startBezierTime;
            e.StartValue = _startBezierValue;
            e.TimeDelta = timeDelta;
            e.ValueDelta = valueDelta;
            e.Bounds = _dragBounds;
            e.ContentHeight = contentRect.height;
            this.Send(e);
        }

        private bool TryGetKeyframeAtPosition(ValueBounds bounds, Vector2 pos, out KeyframeData result) {
            result = default;

            const float tolerance = KEYFRAME_SIZE * 1.5f;

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible || propertyData.Hidden) continue;
                foreach (var keyframe in propertyData.Keyframes) {
                    float x = _data.TimeToPixel(keyframe.Time);
                    float y = bounds.ValueToPixel(keyframe.Value, contentRect.height);
                    Vector2 keyframePos = new(x, y);
                    if (Vector2.Distance(pos, keyframePos) < tolerance) {
                        result = new KeyframeData(type, keyframe);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetBezierHandleAtPosition(
            ValueBounds bounds,
            Vector2 pos,
            out KeyframeData result,
            out bool isBezierOutHandle
        ) {
            result = default;
            isBezierOutHandle = false;

            const float tolerance = HANDLE_SIZE * 1.5f;

            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible || propertyData.Hidden) continue;
                var keyframes = propertyData.Keyframes;
                for (int i = 0; i < keyframes.Count; i++) {
                    var keyframe = keyframes[i];

                    if (!keyframe.Selected) continue;

                    if (
                        keyframe.OutInterpolation == InterpolationType.Bezier &&
                        i < keyframes.Count - 1
                    ) {
                        var next = keyframes[i + 1];
                        float dt = next.Time - keyframe.Time;
                        float outTime = keyframe.Time + (dt * keyframe.OutWeight);
                        float outValue = keyframe.Value + (keyframe.OutTangent * dt * keyframe.OutWeight);

                        float x = _data.TimeToPixel(outTime);
                        float y = bounds.ValueToPixel(outValue, contentRect.height);
                        Vector2 handlePos = new(x, y);

                        if (Vector2.Distance(pos, handlePos) < tolerance) {
                            result = new KeyframeData(type, keyframe);
                            isBezierOutHandle = true;
                            return true;
                        }
                    }

                    if (
                        keyframe.InInterpolation == InterpolationType.Bezier &&
                        i > 0
                    ) {
                        var prev = keyframes[i - 1];
                        float dt = keyframe.Time - prev.Time;
                        float inTime = keyframe.Time - (dt * keyframe.InWeight);
                        float inValue = keyframe.Value - (keyframe.InTangent * dt * keyframe.InWeight);

                        float x = _data.TimeToPixel(inTime);
                        float y = bounds.ValueToPixel(inValue, contentRect.height);
                        Vector2 handlePos = new(x, y);

                        if (Vector2.Distance(pos, handlePos) < tolerance) {
                            result = new KeyframeData(type, keyframe);
                            isBezierOutHandle = false;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void BoxSelect(Rect rect) {
            _selected.Clear();
            foreach (var (type, propertyData) in _data.Properties) {
                if (!propertyData.Visible || propertyData.Hidden) continue;

                foreach (var keyframe in propertyData.Keyframes) {
                    float keyframeX = _data.TimeToPixel(keyframe.Time);
                    float keyframeY = _data.ValueBounds.ValueToPixel(keyframe.Value, contentRect.height);

                    Vector2 keyframePos = new(keyframeX, keyframeY);
                    if (rect.Contains(keyframePos)) {
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
