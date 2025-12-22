using KexEdit.Legacy;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public class KeyframeFloatField : VisualElement {
        private FloatField _field;
        private Label _label;
        private KeyframeFieldType _fieldType;

        private TimelineData _data;
        private float _startPosition;
        private float _startValue;
        private float _lastScrollTime;
        private bool _dragging;
        private bool _moved;

        public KeyframeFloatField(string labelText, KeyframeFieldType fieldType) {
            _fieldType = fieldType;

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = 6f;

            _label = new Label(labelText) {
                style = {
                    color = s_TextColor,
                    width = 80f,
                }
            };
            Add(_label);

            _field = new FloatField {
                formatString = "0.###",
                isDelayed = true,
                style = {
                    flexGrow = 1f,
                    marginLeft = 8f
                }
            };
            Add(_field);

            _label.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _label.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _label.RegisterCallback<MouseUpEvent>(OnMouseUp);
            _field.RegisterCallback<ChangeEvent<float>>(OnFieldValueChanged);
            _field.RegisterCallback<WheelEvent>(OnFieldScroll);
        }

        public void Initialize(TimelineData data) {
            _data = data;

            _label.style.cursor = UIService.SlideHorizontalCursor;
        }

        public void UpdateValue() {
            float displayValue;
            switch (_fieldType) {
                case KeyframeFieldType.Value:
                    UnitsType units = _data.EditingKeyframeType.GetUnits(_data.DurationType);
                    displayValue = units.ValueToDisplay(_data.EditingKeyframeValue);
                    break;
                case KeyframeFieldType.Time:
                    displayValue = _data.DurationType switch {
                        DurationType.Distance => Units.DistanceToDisplay(_data.EditingKeyframeTime),
                        _ => _data.EditingKeyframeTime,
                    };
                    break;
                case KeyframeFieldType.InWeight:
                    displayValue = _data.EditingKeyframeInWeight;
                    break;
                case KeyframeFieldType.InTangent:
                    displayValue = _data.EditingKeyframeInTangent;
                    break;
                case KeyframeFieldType.OutWeight:
                    displayValue = _data.EditingKeyframeOutWeight;
                    break;
                case KeyframeFieldType.OutTangent:
                    displayValue = _data.EditingKeyframeOutTangent;
                    break;
                default:
                    displayValue = 0f;
                    break;
            }
            _field.SetValueWithoutNotify(displayValue);
        }

        private void SetInternalValue(float displayValue) {
            switch (_fieldType) {
                case KeyframeFieldType.Value:
                    UnitsType units = _data.EditingKeyframeType.GetUnits(_data.DurationType);
                    float newValue = units.DisplayToValue(displayValue);
                    if (math.abs(newValue - _data.EditingKeyframeValue) < 1e-3f) return;
                    _data.EditingKeyframeValue = newValue;
                    break;
                case KeyframeFieldType.Time:
                    float newTime = _data.DurationType switch {
                        DurationType.Distance => Units.DisplayToDistance(displayValue),
                        _ => displayValue,
                    };
                    if (math.abs(newTime - _data.EditingKeyframeTime) < 1e-3f) return;
                    _data.EditingKeyframeTime = newTime;
                    break;
                case KeyframeFieldType.InWeight:
                    float inWeight = math.clamp(displayValue, 0.01f, 2f);
                    if (math.abs(inWeight - _data.EditingKeyframeInWeight) < 1e-3f) return;
                    _data.EditingKeyframeInWeight = inWeight;
                    break;
                case KeyframeFieldType.InTangent:
                    float inTangent = displayValue;
                    if (math.abs(inTangent - _data.EditingKeyframeInTangent) < 1e-3f) return;
                    _data.EditingKeyframeInTangent = inTangent;
                    break;
                case KeyframeFieldType.OutWeight:
                    float outWeight = math.clamp(displayValue, 0.01f, 2f);
                    if (math.abs(outWeight - _data.EditingKeyframeOutWeight) < 1e-3f) return;
                    _data.EditingKeyframeOutWeight = outWeight;
                    break;
                case KeyframeFieldType.OutTangent:
                    float outTangent = displayValue;
                    if (math.abs(outTangent - _data.EditingKeyframeOutTangent) < 1e-3f) return;
                    _data.EditingKeyframeOutTangent = outTangent;
                    break;
            }

            PropagateKeyframeChanges();
        }

        private void PropagateKeyframeChanges() {
            if (!_data.Active || !_data.HasEditingKeyframe) return;

            var e = this.GetPooled<SetKeyframeAtTimeEvent>();
            e.Type = _data.EditingKeyframeType;
            e.KeyframeId = _data.EditingKeyframeId;
            e.Time = _data.EditingKeyframeTime;
            e.Value = _data.EditingKeyframeValue;
            e.InInterpolation = _data.EditingKeyframeInInterpolation;
            e.OutInterpolation = _data.EditingKeyframeOutInterpolation;
            e.InWeight = _data.EditingKeyframeInWeight;
            e.InTangent = _data.EditingKeyframeInTangent;
            e.OutWeight = _data.EditingKeyframeOutWeight;
            e.OutTangent = _data.EditingKeyframeOutTangent;
            this.Send(e);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            _startPosition = evt.mousePosition.x;
            _startValue = _field.value;
            _dragging = true;
            _moved = false;
            _label.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;

            float delta = evt.mousePosition.x - _startPosition;
            delta *= 1e-2f;

            if (!_moved && math.abs(delta) > 1e-6f) {
                _moved = true;
                Undo.Record();
            }

            float value = _startValue + delta;
            value = math.round(value * 1e3f) / 1e3f;

            _field.SetValueWithoutNotify(value);
            SetInternalValue(value);

            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_dragging) return;

            _dragging = false;
            _label.ReleaseMouse();
            evt.StopPropagation();
        }

        private void OnFieldValueChanged(ChangeEvent<float> evt) {
            Undo.Record();
            SetInternalValue(evt.newValue);
        }

        private void OnFieldScroll(WheelEvent evt) {
            float scrollAmount = evt.shiftKey ? 0.01f : 0.1f;
            float delta = evt.delta.y > 0 ? -scrollAmount : scrollAmount;
            float newValue = _field.value + delta;

            newValue = ApplyFieldConstraints(newValue);
            float roundedValue = math.round(newValue * 1e3f) / 1e3f;

            if (math.abs(roundedValue - _field.value) > 1e-6f) {
                float currentTime = Time.realtimeSinceStartup;
                if (currentTime - _lastScrollTime > 0.5f) {
                    Undo.Record();
                }
                _lastScrollTime = currentTime;

                _field.SetValueWithoutNotify(roundedValue);
                SetInternalValue(roundedValue);
            }

            evt.StopPropagation();
        }

        private float ApplyFieldConstraints(float value) {
            return _fieldType switch {
                KeyframeFieldType.InWeight or KeyframeFieldType.OutWeight => math.clamp(value, 0.01f, 2f),
                _ => value,
            };
        }
    }
}
