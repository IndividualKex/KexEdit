using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;
using static KexEdit.UI.Extensions;

namespace KexEdit.UI.Timeline {
    public class KeyframeEditDialog : VisualElement {

        private readonly KeyframeData _originalKeyframe;
        private readonly DurationType _durationType;
        private readonly Action<Keyframe> _onApply;
        private readonly Action _onClose;

        private VisualElement _panel;
        private FloatField _valueField;
        private FloatField _timeField;
        private FloatField _inWeightField;
        private FloatField _inTangentField;
        private FloatField _outWeightField;
        private FloatField _outTangentField;
        private VisualElement _easingButtonContainer;

        private EasingType? _selectedEasing;

        public KeyframeEditDialog(KeyframeData keyframe, DurationType durationType, Action<Keyframe> onApply, Action onClose) {
            _originalKeyframe = keyframe;
            _durationType = durationType;
            _onApply = onApply;
            _onClose = onClose;

            focusable = true;

            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            style.backgroundColor = new Color(0, 0, 0, 0.5f);
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            CreatePanel();
            InitializeFields();
            SetupEventHandlers();

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<MouseDownEvent>(OnBackgroundClick, TrickleDown.TrickleDown);

            schedule.Execute(() => { _panel.style.opacity = 1f; });
        }

        private void CreatePanel() {
            _panel = new VisualElement {
                style = {
                    backgroundColor = s_BackgroundColor,
                    borderTopLeftRadius = 3f, borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f,
                    borderTopWidth = 1f, borderRightWidth = 1f, borderBottomWidth = 1f, borderLeftWidth = 1f,
                    borderTopColor = s_BorderColor, borderRightColor = s_BorderColor,
                    borderBottomColor = s_BorderColor, borderLeftColor = s_BorderColor,
                    paddingTop = 16f, paddingRight = 16f, paddingBottom = 16f, paddingLeft = 16f,
                    minWidth = 300f,
                    opacity = 0f,
                    transitionProperty = new List<StylePropertyName> { "opacity" },
                    transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) },
                    transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic }
                }
            };

            var title = new Label("Edit Keyframe") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 16f, unityTextAlign = TextAnchor.UpperCenter
                }
            };

            _panel.Add(title);
            Add(_panel);
        }

        private void InitializeFields() {
            var unitsType = _originalKeyframe.Type.GetUnits(_durationType);
            var timeUnitsType = _durationType == DurationType.Time ? UnitsType.Time : UnitsType.Distance;

            AddFieldRow("Value:", _originalKeyframe.Value.Value, unitsType, out _valueField);
            AddFieldRow("Time:", _originalKeyframe.Value.Time, timeUnitsType, out _timeField);

            AddSeparator();

            AddFieldRow("In Weight:", _originalKeyframe.Value.InWeight, UnitsType.None, out _inWeightField);
            AddFieldRow("In Tangent:", _originalKeyframe.Value.InTangent, UnitsType.None, out _inTangentField);
            AddFieldRow("Out Weight:", _originalKeyframe.Value.OutWeight, UnitsType.None, out _outWeightField);
            AddFieldRow("Out Tangent:", _originalKeyframe.Value.OutTangent, UnitsType.None, out _outTangentField);

            AddEasingField();
            AddButtons();
        }

        private void AddFieldRow(string labelText, float value, UnitsType unitsType, out FloatField field) {
            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f
                }
            };

            var label = new Label(labelText) {
                style = { fontSize = 12, color = s_TextColor, width = 80f }
            };

            field = new FloatField {
                value = value,
                isDelayed = true,
                style = { flexGrow = 1f, marginLeft = 8f }
            };

            if (unitsType != UnitsType.None) {
                var unitsLabel = new Label(unitsType.ToDisplaySuffix()) {
                    style = { fontSize = 12, color = s_TextColor, marginLeft = 4f }
                };
                container.Add(label);
                container.Add(field);
                container.Add(unitsLabel);
            }
            else {
                container.Add(label);
                container.Add(field);
            }

            _panel.Add(container);
        }

        private void AddSeparator() {
            var separator = new VisualElement {
                style = {
                    height = 1f,
                    backgroundColor = s_BorderColor,
                    marginTop = 8f,
                    marginBottom = 8f
                }
            };
            _panel.Add(separator);
        }

        private void AddEasingField() {
            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column,
                    marginBottom = 16f
                }
            };

            var label = new Label("Easing:") {
                style = { fontSize = 12, color = s_TextColor, marginBottom = 8f }
            };

            _easingButtonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    justifyContent = Justify.FlexStart
                }
            };

            DetectCurrentEasing();
            CreateEasingButtons();

            container.Add(label);
            container.Add(_easingButtonContainer);
            _panel.Add(container);
        }

        private void AddButtons() {
            var buttonContainer = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd }
            };

            var cancelButton = CreateButton("Cancel", Close);
            var applyButton = CreateButton("Apply", Apply);

            buttonContainer.Add(cancelButton);
            buttonContainer.Add(applyButton);
            _panel.Add(buttonContainer);
        }

        private Label CreateButton(string text, Action onClick) {
            var button = new Label(text) {
                style = {
                    marginLeft = 8f,
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    borderTopLeftRadius = 3f, borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f,
                }
            };

            button.RegisterCallback<MouseEnterEvent>(_ => button.style.backgroundColor = s_HoverColor);
            button.RegisterCallback<MouseLeaveEvent>(_ => button.style.backgroundColor = s_BackgroundColor);
            button.RegisterCallback<MouseDownEvent>(_ => onClick?.Invoke());

            return button;
        }

        private void SetupEventHandlers() {
            _inWeightField.RegisterCallback<ChangeEvent<float>>(evt => {
                _inWeightField.value = Mathf.Clamp(evt.newValue, 0.01f, 2f);
                UpdateEasingButtonStates();
            });

            _outWeightField.RegisterCallback<ChangeEvent<float>>(evt => {
                _outWeightField.value = Mathf.Clamp(evt.newValue, 0.01f, 2f);
                UpdateEasingButtonStates();
            });

            _inTangentField.RegisterCallback<ChangeEvent<float>>(evt => {
                UpdateEasingButtonStates();
            });

            _outTangentField.RegisterCallback<ChangeEvent<float>>(evt => {
                UpdateEasingButtonStates();
            });
        }

        private void DetectCurrentEasing() {
            _selectedEasing = GetEasingFromWeights(_originalKeyframe.Value.InWeight, _originalKeyframe.Value.OutWeight);
        }

        private void CreateEasingButtons() {
            foreach (var easingType in Extensions.EnumerateEasingTypes()) {
                var button = CreateEasingButton(easingType.GetDisplayName(), easingType);
                _easingButtonContainer.Add(button);
            }
        }

        private Label CreateEasingButton(string text, EasingType easingType) {
            var button = new Label(text) {
                style = {
                    marginRight = 4f,
                    marginBottom = 4f,
                    paddingTop = 6f, paddingRight = 8f, paddingBottom = 6f, paddingLeft = 8f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 11
                }
            };

            bool isSelected = _selectedEasing == easingType;
            UpdateEasingButtonAppearance(button, isSelected);

            button.RegisterCallback<MouseEnterEvent>(_ => {
                if (_selectedEasing != easingType) {
                    button.style.backgroundColor = s_HoverColor;
                }
            });

            button.RegisterCallback<MouseLeaveEvent>(_ => {
                UpdateEasingButtonAppearance(button, _selectedEasing == easingType);
            });

            button.RegisterCallback<MouseDownEvent>(_ => {
                _selectedEasing = easingType;
                ApplyEasing();
                UpdateEasingButtonStates();
            });

            return button;
        }

        private void UpdateEasingButtonAppearance(Label button, bool isSelected) {
            if (isSelected) {
                button.style.backgroundColor = s_ActiveColor;
                button.style.color = s_ActiveTextColor;
            } else {
                button.style.backgroundColor = s_BackgroundColor;
                button.style.color = s_TextColor;
            }
        }

        private void UpdateEasingButtonStates() {
            var detectedEasing = GetEasingFromWeights(_inWeightField.value, _outWeightField.value);
            
            int buttonIndex = 0;
            foreach (var easingType in Extensions.EnumerateEasingTypes()) {
                var button = _easingButtonContainer[buttonIndex] as Label;
                bool isSelected = detectedEasing == easingType;
                UpdateEasingButtonAppearance(button, isSelected);
                buttonIndex++;
            }
        }

        private void ApplyEasing() {
            if (!_selectedEasing.HasValue) return;

            _selectedEasing.Value.GetEasingHandles(out float tangent, out float weight, out _, out _);
            _inWeightField.value = weight;
            _outWeightField.value = weight;
            _inTangentField.value = tangent;
            _outTangentField.value = tangent;
        }

        private void OnKeyDown(KeyDownEvent evt) {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) {
                Apply();
            }
            else if (evt.keyCode == KeyCode.Escape) {
                Close();
            }
            evt.StopPropagation();
        }

        private void OnBackgroundClick(MouseDownEvent evt) {
            if (evt.target == this) {
                Close();
            }
        }

        private void Apply() {
            var updatedKeyframe = new Keyframe {
                Id = _originalKeyframe.Value.Id,
                Time = _timeField.value,
                Value = _valueField.value,
                InInterpolation = _originalKeyframe.Value.InInterpolation,
                OutInterpolation = _originalKeyframe.Value.OutInterpolation,
                HandleType = _originalKeyframe.Value.HandleType,
                Flags = _originalKeyframe.Value.Flags,
                InTangent = _inTangentField.value,
                OutTangent = _outTangentField.value,
                InWeight = _inWeightField.value,
                OutWeight = _outWeightField.value,
                Selected = _originalKeyframe.Value.Selected
            };

            _onApply?.Invoke(updatedKeyframe);
            Close();
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
