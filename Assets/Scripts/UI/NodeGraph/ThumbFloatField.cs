using Unity.Mathematics;
using Unity.Properties;
using UnityEngine.UIElements;

namespace KexEdit.UI.NodeGraph {
    public class ThumbFloatField : VisualElement {
        private static readonly string[] s_DisplayNames = new string[] { "X", "Y", "Z" };

        private FloatField _field;
        private Label _label;

        private PortData _data;
        private float _startPosition;
        private float _startValue;
        private int _index;
        private bool _dragging;
        private bool _moved;

        public ThumbFloatField(PortData data, int index) {
            _data = data;
            _index = index;

            style.position = Position.Relative;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;

            var dummy = new VisualElement {
                style = {
                    position = Position.Relative,
                    minWidth = 3f,
                }
            };
            Add(dummy);

            _label = new Label(s_DisplayNames[index]) {
                style = {
                    cursor = UIService.SlideHorizontalCursor
                }
            };
            dummy.Add(_label);

            _field = new FloatField {
                formatString = "0.###",
                isDelayed = true,
                style = {
                    width = 30f,
                    marginLeft = 4f,
                    marginRight = 4f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 1f,
                    paddingBottom = 1f
                }
            };
            Add(_field);

            _label.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _label.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _label.RegisterCallback<MouseUpEvent>(OnMouseUp);

            var binding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PortData.Value)),
                bindingMode = BindingMode.ToTarget
            };
            binding.sourceToUiConverters.AddConverter((ref PointData value) => {
                float floatValue = _index switch {
                    0 => value.Roll,
                    1 => value.Velocity,
                    2 => value.Energy,
                    _ => throw new System.NotImplementedException(),
                };
                return _data.Units.ValueToDisplay(floatValue);
            });
            _field.SetBinding("value", binding);
            _field.RegisterValueChangedCallback<float>(OnFieldValueChanged);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            _startPosition = evt.mousePosition.x;
            _startValue = _data.Units.DisplayToValue(_field.value);
            _dragging = true;
            _moved = false;
            _label.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;

            var bounds = _data.Port.Type.GetPortBounds();

            float delta = evt.mousePosition.x - _startPosition;
            delta = _data.Units.DisplayToValue(delta);

            if (!_moved && math.abs(delta) > 1e-3f) {
                _moved = true;
                Undo.Record();
            }

            float value = _startValue + delta * 1e-2f;

            value = math.round(value * 1e3f) / 1e3f;
            value = math.clamp(value, bounds.Min, bounds.Max);

            float displayValue = _data.Units.ValueToDisplay(value);
            _field.SetValueWithoutNotify(displayValue);
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
            float internalValue = _data.Units.DisplayToValue(evt.newValue);
            if (internalValue == GetValue()) return;
            Undo.Record();
            SetInternalValue(internalValue);
        }

        private void SetInternalValue(float internalValue) {
            switch (_index) {
                case 0:
                    _data.Value.Roll = internalValue;
                    break;
                case 1:
                    _data.Value.Velocity = internalValue;
                    break;
                case 2:
                    _data.Value.Energy = internalValue;
                    break;
                default:
                    throw new System.NotImplementedException();
            }

            var e = this.GetPooled<PortChangeEvent>();
            e.Port = _data;
            this.Send(e);
        }

        private float GetValue() {
            return _index switch {
                0 => _data.Value.Roll,
                1 => _data.Value.Velocity,
                2 => _data.Value.Energy,
                _ => throw new System.NotImplementedException(),
            };
        }
    }
}
