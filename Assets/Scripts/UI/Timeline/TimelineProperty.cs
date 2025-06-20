using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public class TimelineProperty : VisualElement {
        private Label _nameLabel;
        private Label _unitLabel;
        private FloatField _valueField;
        private ArrowButton _leftArrowButton;
        private KeyframeButton _keyframeButton;
        private ArrowButton _rightArrowButton;
        private string _propertyName;

        private PropertyData _data;
        private float _prevX;
        private float _sensitivity = 0.01f;
        private float _rawValue;
        private float _lastScrollTime;
        private bool _dragging;
        private bool _undoRecorded;

        public TimelineProperty(PropertyData data) {
            _data = data;
            dataSource = data;

            _propertyName = data.Type.GetDisplayName();

            style.flexDirection = FlexDirection.Row;
            style.height = 20f;

            var selectedOverlay = new VisualElement {
                name = "SelectedOverlay",
                style = {
                    position = Position.Absolute,
                    left = 0, top = 0, right = 0, bottom = 0,
                    backgroundColor = s_BackgroundColor,
                    opacity = 0f,
                    transitionProperty = new List<StylePropertyName> { "opacity" },
                    transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) },
                    transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic }
                }
            };
            Add(selectedOverlay);

            var indicatorContainer = new VisualElement {
                style = {
                    width = 10f,
                    height = 10f,
                    marginLeft = 8f,
                    marginRight = 4f,
                    alignSelf = Align.Center,
                    position = Position.Relative
                }
            };

            var colorIndicator = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0, top = 0, right = 0, bottom = 0,
                    backgroundColor = data.Type.GetColor(),
                    borderTopLeftRadius = 5f,
                    borderTopRightRadius = 5f,
                    borderBottomLeftRadius = 5f,
                    borderBottomRightRadius = 5f,
                    opacity = 1f,
                    transitionProperty = new List<StylePropertyName> { "opacity" },
                    transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) },
                    transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic }
                }
            };
            indicatorContainer.Add(colorIndicator);

            if (data.IsRemovable) {
                var removeButton = new Label("×") {
                    style = {
                        position = Position.Absolute,
                        left = 0, top = 0, right = 0, bottom = 0,
                        color = Color.white,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        opacity = 0f,
                        transitionProperty = new List<StylePropertyName> { "opacity" },
                        transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) },
                        transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic }
                    }
                };

                this.RegisterCallback<MouseEnterEvent>(_ => {
                    colorIndicator.style.opacity = 0f;
                    removeButton.style.opacity = 1f;
                });

                this.RegisterCallback<MouseLeaveEvent>(_ => {
                    colorIndicator.style.opacity = 1f;
                    removeButton.style.opacity = 0f;
                });

                removeButton.RegisterCallback<MouseDownEvent>(evt => {
                    if (evt.button == 0) {
                        var e = this.GetPooled<RemovePropertyClickEvent>();
                        e.Type = data.Type;
                        this.Send(e);
                        evt.StopPropagation();
                    }
                });

                indicatorContainer.Add(removeButton);
            }

            Add(indicatorContainer);

            var nameContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1f,
                    alignItems = Align.Center,
                    paddingLeft = 4f,
                    paddingRight = 8f,
                }
            };

            _nameLabel = new Label(data.Type.GetDisplayName()) {
                style = {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    fontSize = 12,
                    color = new Color(0.6f, 0.6f, 0.6f)
                }
            };
            nameContainer.Add(_nameLabel);

            _unitLabel = new Label("") {
                style = {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    fontSize = 11,
                    color = new Color(0.4f, 0.4f, 0.4f)
                }
            };
            nameContainer.Add(_unitLabel);

            Add(nameContainer);

            var valueContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    width = 56f,
                    marginRight = 4f,
                    marginTop = 2f,
                    marginBottom = 2f,
                }
            };

            var slidingArea = new VisualElement {
                style = {
                    width = 8f,
                    height = Length.Percent(100),
                    cursor = UIService.SlideHorizontalCursor
                }
            };
            slidingArea.RegisterCallback<MouseDownEvent>(OnLabelMouseDown);
            slidingArea.RegisterCallback<MouseMoveEvent>(OnLabelMouseMove);
            slidingArea.RegisterCallback<MouseUpEvent>(OnLabelMouseUp);
            valueContainer.Add(slidingArea);

            _valueField = new FloatField {
                formatString = "0.###",
                isDelayed = true,
                style = {
                    flexGrow = 1f,
                    marginRight = 4f,
                    cursor = UIService.TextCursor
                }
            };
            _valueField.RegisterValueChangedCallback<float>(OnValueChanged);
            _valueField.RegisterCallback<WheelEvent>(OnValueFieldScroll);
            valueContainer.Add(_valueField);

            Add(valueContainer);

            _leftArrowButton = new ArrowButton(false);
            _leftArrowButton.OnClicked += OnLeftArrowClicked;
            Add(_leftArrowButton);

            _keyframeButton = new KeyframeButton(_data);
            Add(_keyframeButton);

            _rightArrowButton = new ArrowButton(true) {
                style = {
                    marginRight = 4f
                }
            };
            _rightArrowButton.OnClicked += OnRightArrowClicked;
            Add(_rightArrowButton);

            RegisterCallback<MouseDownEvent>(OnMouseDown);

            var altBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PropertyData.IsAlt)),
                bindingMode = BindingMode.ToTarget
            };
            altBinding.sourceToUiConverters.AddConverter((ref bool value) => new StyleColor(value ? s_AltDarkBackgroundColor : s_DarkBackgroundColor));
            SetBinding("style.backgroundColor", altBinding);

            var selectedBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PropertyData.Selected)),
                bindingMode = BindingMode.ToTarget
            };
            selectedBinding.sourceToUiConverters.AddConverter((ref bool value) => new StyleFloat(value ? 1f : 0f));
            selectedOverlay.SetBinding("style.opacity", selectedBinding);

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PropertyData.Visible)),
                bindingMode = BindingMode.ToTarget
            };
            displayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleEnum<DisplayStyle>(value ? DisplayStyle.Flex : DisplayStyle.None));
            SetBinding("style.display", displayBinding);

            var valueBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PropertyData.Value)),
                bindingMode = BindingMode.ToTarget
            };
            valueBinding.sourceToUiConverters.AddConverter((ref float value) => {
                return _data.Units.ValueToDisplay(value);
            });
            _valueField.SetBinding("value", valueBinding);

            var unitBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PropertyData.Units)),
                bindingMode = BindingMode.ToTarget
            };
            unitBinding.sourceToUiConverters.AddConverter((ref UnitsType value) => value.ToDisplayString());
            _unitLabel.SetBinding("text", unitBinding);
        }

        private void OnLabelMouseDown(MouseDownEvent evt) {
            if (evt.button == 0) {
                _dragging = true;
                _undoRecorded = false;
                ((VisualElement)evt.target).CaptureMouse();
                _prevX = evt.mousePosition.x;
                _rawValue = _data.Units.DisplayToValue(_valueField.value);
                evt.StopPropagation();
            }
        }

        private void OnLabelMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;

            float deltaX = evt.mousePosition.x - _prevX;
            deltaX = _data.Units.DisplayToValue(deltaX);
            _prevX = evt.mousePosition.x;

            _rawValue += deltaX * _sensitivity;

            float roundedValue = math.round(_rawValue * 1e3f) / 1e3f;

            if (math.abs(roundedValue - _data.Value) > 1e-6f) {
                if (!_undoRecorded) {
                    Undo.Record();
                    _undoRecorded = true;
                }
                float displayValue = _data.Units.ValueToDisplay(roundedValue);
                _valueField.SetValueWithoutNotify(displayValue);
                var e = this.GetPooled<SetKeyframeEvent>();
                e.Type = _data.Type;
                e.Value = roundedValue;
                this.Send(e);
            }

            evt.StopPropagation();
        }

        private void OnLabelMouseUp(MouseUpEvent evt) {
            if (!_dragging) return;

            _dragging = false;
            ((VisualElement)evt.target).ReleaseMouse();
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0 || evt.button == 1) {
                var e = this.GetPooled<PropertyClickEvent>();
                e.Type = _data.Type;
                e.ShiftKey = evt.shiftKey;
                this.Send(e);
                evt.StopPropagation();
            }

            if (evt.button == 1 && !evt.altKey && _data.IsRemovable) {
                var e = this.GetPooled<PropertyRightClickEvent>();
                e.Type = _data.Type;
                e.MousePosition = evt.localMousePosition;
                this.Send(e);
                evt.StopPropagation();
            }
        }

        private void OnValueChanged(ChangeEvent<float> evt) {
            float newValue = _data.Units.DisplayToValue(evt.newValue);
            if (math.abs(newValue - _data.Value) < 1e-6f) return;
            Undo.Record();
            var e = this.GetPooled<SetKeyframeEvent>();
            e.Type = _data.Type;
            e.Value = newValue;
            this.Send(e);
        }

        private void OnValueFieldScroll(WheelEvent evt) {
            float scrollAmount = evt.shiftKey ? 0.01f : 0.1f;
            float delta = evt.delta.y > 0 ? -scrollAmount : scrollAmount;
            float newValue = _valueField.value + delta;

            float roundedValue = math.round(newValue * 1e3f) / 1e3f;
            float internalValue = _data.Units.DisplayToValue(roundedValue);

            if (math.abs(internalValue - _data.Value) > 1e-6f) {
                float currentTime = Time.realtimeSinceStartup;
                if (currentTime - _lastScrollTime > 0.5f) {
                    Undo.Record();
                }
                _lastScrollTime = currentTime;

                _valueField.SetValueWithoutNotify(roundedValue);
                var e = this.GetPooled<SetKeyframeEvent>();
                e.Type = _data.Type;
                e.Value = internalValue;
                this.Send(e);
            }

            evt.StopPropagation();
        }

        private void OnLeftArrowClicked() {
            var e = this.GetPooled<JumpToKeyframeEvent>();
            e.Type = _data.Type;
            e.Direction = NavigationDirection.Previous;
            this.Send(e);
        }

        private void OnRightArrowClicked() {
            var e = this.GetPooled<JumpToKeyframeEvent>();
            e.Type = _data.Type;
            e.Direction = NavigationDirection.Next;
            this.Send(e);
        }
    }
}
