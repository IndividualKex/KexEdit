using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;
using static KexEdit.UI.Extensions;

namespace KexEdit.UI.Timeline {
    public class KeyframeEditor : VisualElement {
        private VisualElement _inEasingButtonContainer;
        private VisualElement _outEasingButtonContainer;
        private VisualElement _inInterpolationContainer;
        private VisualElement _outInterpolationContainer;
        private KeyframeFloatField _valueField;
        private KeyframeFloatField _timeField;
        private KeyframeFloatField _inWeightField;
        private KeyframeFloatField _inTangentField;
        private KeyframeFloatField _outWeightField;
        private KeyframeFloatField _outTangentField;
        private Label _valueUnitsLabel;
        private Label _timeUnitsLabel;
        private TimelineData _data;
        private EasingType? _selectedInEasing;
        private EasingType? _selectedOutEasing;

        public KeyframeEditor() {
            focusable = true;
            style.position = Position.Relative;
            style.width = 420f;
            style.backgroundColor = s_DarkBackgroundColor;

            var tip = new Label("Select a keyframe to edit") {
                style = {
                    position = Position.Absolute,
                    top = 0f,
                    left = 0f,
                    right = 0f,
                    bottom = 0f,
                    color = s_ActiveHoverColor,
                    overflow = Overflow.Hidden,
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            Add(tip);

            var panel = new VisualElement {
                style = {
                    paddingTop = 8f, paddingRight = 8f, paddingBottom = 8f, paddingLeft = 8f,
                    height = Length.Percent(100),
                    overflow = Overflow.Hidden,
                    flexDirection = FlexDirection.Column
                }
            };
            Add(panel);

            var title = new Label("Edit Keyframe") {
                style = {
                    color = s_TextColor, marginBottom = 12f
                }
            };
            panel.Add(title);

            _valueField = new KeyframeFloatField("Value:", KeyframeFieldType.Value);
            panel.Add(_valueField);

            _valueUnitsLabel = new Label {
                style = { color = s_TextColor, marginLeft = 4f }
            };
            _valueField.Add(_valueUnitsLabel);

            _timeField = new KeyframeFloatField("Time:", KeyframeFieldType.Time);
            panel.Add(_timeField);

            _timeUnitsLabel = new Label {
                style = { color = s_TextColor, marginLeft = 4f }
            };
            _timeField.Add(_timeUnitsLabel);

            var inSeparator = new VisualElement {
                style = {
                    height = 1f,
                    backgroundColor = s_DividerColor,
                    marginTop = 6f,
                    marginBottom = 12f
                }
            };
            panel.Add(inSeparator);

            CreateInHandleSection(panel);

            var outSeparator = new VisualElement {
                style = {
                    height = 1f,
                    backgroundColor = s_DividerColor,
                    marginTop = 6f,
                    marginBottom = 12f
                }
            };
            panel.Add(outSeparator);

            CreateOutHandleSection(panel);

            var tipDisplayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.HasEditingKeyframe)),
                bindingMode = BindingMode.ToTarget
            };
            tipDisplayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                (StyleEnum<DisplayStyle>)(value ? DisplayStyle.None : DisplayStyle.Flex));
            tip.SetBinding("style.display", tipDisplayBinding);

            var panelDisplayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.HasEditingKeyframe)),
                bindingMode = BindingMode.ToTarget
            };
            panelDisplayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                (StyleEnum<DisplayStyle>)(value ? DisplayStyle.Flex : DisplayStyle.None));
            panel.SetBinding("style.display", panelDisplayBinding);

            var valueBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeValue)),
                bindingMode = BindingMode.ToTarget
            };
            valueBinding.sourceToUiConverters.AddConverter((ref float value) =>
                _data.EditingKeyframeType.GetUnits(_data.DurationType).ValueToDisplay(value));
            _valueField.SetBinding("value", valueBinding);
        }

        public void Initialize(TimelineData data) {
            _data = data;

            _valueField.Initialize(data);
            _timeField.Initialize(data);
            _inWeightField.Initialize(data);
            _inTangentField.Initialize(data);
            _outWeightField.Initialize(data);
            _outTangentField.Initialize(data);
        }

        public void Draw() {
            if (!_data.Active || !_data.HasEditingKeyframe) return;

            _valueField.UpdateValue();
            _timeField.UpdateValue();
            _inWeightField.UpdateValue();
            _inTangentField.UpdateValue();
            _outWeightField.UpdateValue();
            _outTangentField.UpdateValue();

            _valueUnitsLabel.text = _data.EditingKeyframeType.GetUnits(_data.DurationType).ToDisplayString();
            _timeUnitsLabel.text = _data.DurationType == DurationType.Distance ?
                Units.GetDistanceUnitsString() : s_UnitsSeconds;

            UpdateInEasingButtonStates();
            UpdateOutEasingButtonStates();
            UpdateInInterpolationButtonStates();
            UpdateOutInterpolationButtonStates();
        }

        private void CreateInHandleSection(VisualElement parent) {
            var inContainer = new VisualElement();
            parent.Add(inContainer);

            var inTitleContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f,
                    height = 14f
                }
            };
            inContainer.Add(inTitleContainer);

            var inTitle = new Label("In Handle:") {
                style = {
                    color = s_TextColor,
                    width = 80f
                }
            };
            inTitleContainer.Add(inTitle);

            _inInterpolationContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1f
                }
            };
            inTitleContainer.Add(_inInterpolationContainer);

            var constantButton = CreateInterpolationButton("Constant", InterpolationType.Constant, true);
            var linearButton = CreateInterpolationButton("Linear", InterpolationType.Linear, true);
            var bezierButton = CreateInterpolationButton("Bezier", InterpolationType.Bezier, true);

            _inInterpolationContainer.Add(constantButton);
            _inInterpolationContainer.Add(linearButton);
            _inInterpolationContainer.Add(bezierButton);

            var inBezierContainer = new VisualElement();
            inContainer.Add(inBezierContainer);

            _inWeightField = new KeyframeFloatField("Weight:", KeyframeFieldType.InWeight);
            inBezierContainer.Add(_inWeightField);

            _inTangentField = new KeyframeFloatField("Tangent:", KeyframeFieldType.InTangent);
            inBezierContainer.Add(_inTangentField);

            var inEasingContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6f,
                    height = 14f
                }
            };
            inBezierContainer.Add(inEasingContainer);

            var inEasingLabel = new Label("Easing:") {
                style = { color = s_TextColor, width = 80f }
            };
            inEasingContainer.Add(inEasingLabel);

            _inEasingButtonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart,
                    flexGrow = 1f
                }
            };
            inEasingContainer.Add(_inEasingButtonContainer);

            foreach (var easingType in Extensions.EnumerateEasingTypes()) {
                var button = CreateEasingButton(easingType.GetDisplayName(), easingType, true);
                _inEasingButtonContainer.Add(button);
            }

            var inInterpolationBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeInInterpolation)),
                bindingMode = BindingMode.TwoWay
            };
            constantButton.SetBinding("userData", inInterpolationBinding);
            linearButton.SetBinding("userData", inInterpolationBinding);
            bezierButton.SetBinding("userData", inInterpolationBinding);

            var inBezierVisibilityBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeInInterpolation)),
                bindingMode = BindingMode.ToTarget
            };
            inBezierVisibilityBinding.sourceToUiConverters.AddConverter((ref InterpolationType value) =>
                (StyleEnum<DisplayStyle>)(value == InterpolationType.Bezier ? DisplayStyle.Flex : DisplayStyle.None));
            inBezierContainer.SetBinding("style.display", inBezierVisibilityBinding);

            var inWeightBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeInWeight)),
                bindingMode = BindingMode.TwoWay
            };
            _inWeightField.SetBinding("value", inWeightBinding);

            var inTangentBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeInTangent)),
                bindingMode = BindingMode.TwoWay
            };
            _inTangentField.SetBinding("value", inTangentBinding);

        }

        private void CreateOutHandleSection(VisualElement parent) {
            var outContainer = new VisualElement();
            parent.Add(outContainer);

            var outTitleContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f,
                    height = 14f
                }
            };
            outContainer.Add(outTitleContainer);

            var outTitle = new Label("Out Handle:") {
                style = {
                    color = s_TextColor,
                    width = 80f
                }
            };
            outTitleContainer.Add(outTitle);

            _outInterpolationContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1f
                }
            };
            outTitleContainer.Add(_outInterpolationContainer);

            var constantButton = CreateInterpolationButton("Constant", InterpolationType.Constant, false);
            var linearButton = CreateInterpolationButton("Linear", InterpolationType.Linear, false);
            var bezierButton = CreateInterpolationButton("Bezier", InterpolationType.Bezier, false);

            _outInterpolationContainer.Add(constantButton);
            _outInterpolationContainer.Add(linearButton);
            _outInterpolationContainer.Add(bezierButton);

            var outBezierContainer = new VisualElement();
            outContainer.Add(outBezierContainer);

            _outWeightField = new KeyframeFloatField("Weight:", KeyframeFieldType.OutWeight);
            outBezierContainer.Add(_outWeightField);

            _outTangentField = new KeyframeFloatField("Tangent:", KeyframeFieldType.OutTangent);
            outBezierContainer.Add(_outTangentField);

            var outEasingContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6f,
                    height = 14f
                }
            };
            outBezierContainer.Add(outEasingContainer);

            var outEasingLabel = new Label("Easing:") {
                style = { color = s_TextColor, width = 80f }
            };
            outEasingContainer.Add(outEasingLabel);

            _outEasingButtonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart,
                    flexGrow = 1f
                }
            };
            outEasingContainer.Add(_outEasingButtonContainer);

            foreach (var easingType in Extensions.EnumerateEasingTypes()) {
                var button = CreateEasingButton(easingType.GetDisplayName(), easingType, false);
                _outEasingButtonContainer.Add(button);
            }

            var outInterpolationBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeOutInterpolation)),
                bindingMode = BindingMode.TwoWay
            };
            constantButton.SetBinding("userData", outInterpolationBinding);
            linearButton.SetBinding("userData", outInterpolationBinding);
            bezierButton.SetBinding("userData", outInterpolationBinding);

            var outBezierVisibilityBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeOutInterpolation)),
                bindingMode = BindingMode.ToTarget
            };
            outBezierVisibilityBinding.sourceToUiConverters.AddConverter((ref InterpolationType value) =>
                (StyleEnum<DisplayStyle>)(value == InterpolationType.Bezier ? DisplayStyle.Flex : DisplayStyle.None));
            outBezierContainer.SetBinding("style.display", outBezierVisibilityBinding);

            var outWeightBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeOutWeight)),
                bindingMode = BindingMode.TwoWay
            };
            _outWeightField.SetBinding("value", outWeightBinding);

            var outTangentBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EditingKeyframeOutTangent)),
                bindingMode = BindingMode.TwoWay
            };
            _outTangentField.SetBinding("value", outTangentBinding);
        }

        private Label CreateInterpolationButton(string text, InterpolationType interpolationType, bool isInHandle) {
            var button = new Label(text) {
                style = {
                    flexGrow = 1f,
                    marginRight = 4f,
                    paddingLeft = 4f, paddingRight = 4f, paddingTop = 2f, paddingBottom = 2f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                }
            };

            button.RegisterCallback<MouseDownEvent>(_ => OnInterpolationButtonClick(interpolationType, isInHandle));
            button.RegisterCallback<MouseEnterEvent>(_ => OnInterpolationButtonMouseEnter(button));
            button.RegisterCallback<MouseLeaveEvent>(_ => OnInterpolationButtonMouseLeave(button));

            return button;
        }

        private Label CreateEasingButton(string text, EasingType easingType, bool isInHandle) {
            var button = new Label(text) {
                style = {
                    flexGrow = 1f,
                    marginRight = 4f,
                    paddingLeft = 4f, paddingRight = 4f, paddingTop = 2f, paddingBottom = 2f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                }
            };

            button.RegisterCallback<MouseEnterEvent>(_ => OnEasingButtonMouseEnter(button, easingType, isInHandle));
            button.RegisterCallback<MouseLeaveEvent>(_ => OnEasingButtonMouseLeave(button, easingType, isInHandle));
            button.RegisterCallback<MouseDownEvent>(_ => OnEasingButtonClick(easingType, isInHandle));

            return button;
        }

        private void OnInterpolationButtonClick(InterpolationType interpolationType, bool isInHandle) {
            if (isInHandle) {
                _data.EditingKeyframeInInterpolation = interpolationType;
            }
            else {
                _data.EditingKeyframeOutInterpolation = interpolationType;
            }
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

        private void OnInterpolationButtonMouseEnter(Label button) {
            button.style.backgroundColor = s_HoverColor;
        }

        private void OnInterpolationButtonMouseLeave(Label button) {
            button.style.backgroundColor = s_BackgroundColor;
        }

        private void OnEasingButtonMouseEnter(Label button, EasingType easingType, bool isInHandle) {
            var selectedEasing = isInHandle ? _selectedInEasing : _selectedOutEasing;
            if (selectedEasing != easingType) {
                button.style.backgroundColor = s_HoverColor;
            }
        }

        private void OnEasingButtonMouseLeave(Label button, EasingType easingType, bool isInHandle) {
            var selectedEasing = isInHandle ? _selectedInEasing : _selectedOutEasing;
            UpdateEasingButtonAppearance(button, selectedEasing == easingType);
        }

        private void OnEasingButtonClick(EasingType easingType, bool isInHandle) {
            if (isInHandle) {
                _selectedInEasing = easingType;
                ApplyInEasing();
            }
            else {
                _selectedOutEasing = easingType;
                ApplyOutEasing();
            }
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

        private void UpdateEasingButtonAppearance(Label button, bool isSelected) {
            if (isSelected) {
                button.style.backgroundColor = s_ActiveColor;
                button.style.color = s_ActiveTextColor;
            }
            else {
                button.style.backgroundColor = s_BackgroundColor;
                button.style.color = s_TextColor;
            }
        }

        private void UpdateInEasingButtonStates() {
            var detectedEasing = GetEasingFromWeights(_data.EditingKeyframeInWeight, _data.EditingKeyframeInWeight);

            int buttonIndex = 0;
            foreach (var easingType in Extensions.EnumerateEasingTypes()) {
                if (buttonIndex < _inEasingButtonContainer.childCount) {
                    var button = _inEasingButtonContainer[buttonIndex] as Label;
                    bool isSelected = detectedEasing == easingType;
                    UpdateEasingButtonAppearance(button, isSelected);
                }
                buttonIndex++;
            }
        }

        private void UpdateOutEasingButtonStates() {
            var detectedEasing = GetEasingFromWeights(_data.EditingKeyframeOutWeight, _data.EditingKeyframeOutWeight);

            int buttonIndex = 0;
            foreach (var easingType in Extensions.EnumerateEasingTypes()) {
                if (buttonIndex < _outEasingButtonContainer.childCount) {
                    var button = _outEasingButtonContainer[buttonIndex] as Label;
                    bool isSelected = detectedEasing == easingType;
                    UpdateEasingButtonAppearance(button, isSelected);
                }
                buttonIndex++;
            }
        }

        private void ApplyInEasing() {
            _selectedInEasing.Value.GetEasingHandles(out float tangent, out float weight, out _, out _);
            _data.EditingKeyframeInWeight = weight;
            _data.EditingKeyframeInTangent = tangent;
        }

        private void ApplyOutEasing() {
            _selectedOutEasing.Value.GetEasingHandles(out _, out _, out float tangent, out float weight);
            _data.EditingKeyframeOutWeight = weight;
            _data.EditingKeyframeOutTangent = tangent;
        }

        private void UpdateInInterpolationButtonStates() {
            var interpolationTypes = new[] { InterpolationType.Constant, InterpolationType.Linear, InterpolationType.Bezier };
            for (int i = 0; i < interpolationTypes.Length && i < _inInterpolationContainer.childCount; i++) {
                var button = _inInterpolationContainer[i] as Label;
                bool isSelected = _data.EditingKeyframeInInterpolation == interpolationTypes[i];
                UpdateInterpolationButtonAppearance(button, isSelected);
            }
        }

        private void UpdateOutInterpolationButtonStates() {
            var interpolationTypes = new[] { InterpolationType.Constant, InterpolationType.Linear, InterpolationType.Bezier };
            for (int i = 0; i < interpolationTypes.Length && i < _outInterpolationContainer.childCount; i++) {
                var button = _outInterpolationContainer[i] as Label;
                bool isSelected = _data.EditingKeyframeOutInterpolation == interpolationTypes[i];
                UpdateInterpolationButtonAppearance(button, isSelected);
            }
        }

        private void UpdateInterpolationButtonAppearance(Label button, bool isSelected) {
            if (isSelected) {
                button.style.backgroundColor = s_ActiveColor;
                button.style.color = s_ActiveTextColor;
            }
            else {
                button.style.backgroundColor = s_BackgroundColor;
                button.style.color = s_TextColor;
            }
        }
    }
}
