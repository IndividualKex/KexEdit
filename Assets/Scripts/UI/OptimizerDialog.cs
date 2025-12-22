using System;
using System.Collections.Generic;
using System.Text;
using KexEdit.Legacy;
using KexEdit.UI.Timeline;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class OptimizerDialog : VisualElement {
        private static readonly Dictionary<TargetValueType, string> _targetValueTypeLabels = new() {
            { TargetValueType.Roll, "Roll" },
            { TargetValueType.Pitch, "Pitch" },
            { TargetValueType.Yaw, "Yaw" },
            { TargetValueType.X, "X" },
            { TargetValueType.Y, "Y" },
            { TargetValueType.Z, "Z" },
            { TargetValueType.NormalForce, "Normal Force" },
            { TargetValueType.LateralForce, "Lateral Force" },
        };

        private static readonly Dictionary<DurationType, string> _durationTypeUnits = new() {
            { DurationType.Time, "s" },
            { DurationType.Distance, "m" },
        };

        private Label _title;
        private FloatField _targetField;
        private Label _unitLabel;

        private Action _onClose;
        private OptimizerData _data;
        private StringBuilder _sb = new();

        public OptimizerDialog(Action onClose, OptimizerData data) {
            _onClose = onClose;
            _data = data;
            dataSource = _data;

            focusable = true;

            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            style.backgroundColor = new Color(0, 0, 0, 0.5f);
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            var panel = new VisualElement {
                style = {
                    backgroundColor = s_BackgroundColor,
                    borderTopLeftRadius = 3f, borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f,
                    borderTopWidth = 1f, borderRightWidth = 1f, borderBottomWidth = 1f, borderLeftWidth = 1f,
                    borderTopColor = s_BorderColor, borderRightColor = s_BorderColor,
                    borderBottomColor = s_BorderColor, borderLeftColor = s_BorderColor,
                    paddingTop = 20f, paddingRight = 20f, paddingBottom = 20f, paddingLeft = 20f,
                    minWidth = 300f,
                    alignItems = Align.Center
                }
            };

            _title = new Label($"Optimize {_data.PropertyType.GetDisplayName()}") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor,
                    marginBottom = 16f,
                    unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var inputContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    marginBottom = 12f
                }
            };

            var targetLabel = new Label($"Target {_targetValueTypeLabels[_data.ValueType]}:") {
                style = {
                    fontSize = 12,
                    color = s_TextColor,
                    marginRight = 8f,
                }
            };

            _targetField = new FloatField {
                formatString = "0.###",
                isDelayed = true,
                style = {
                    minWidth = 48f,
                    fontSize = 12
                }
            };

            _unitLabel = new Label(_data.Units.ToDisplayString()) {
                style = {
                    fontSize = 11,
                    color = new Color(0.4f, 0.4f, 0.4f),
                    marginLeft = 4f
                }
            };

            var timeLabel = new Label(FormatTimeString()) {
                style = {
                    fontSize = 12,
                    color = s_TextColor,
                    marginLeft = 8f
                }
            };

            inputContainer.Add(targetLabel);
            inputContainer.Add(_targetField);
            inputContainer.Add(_unitLabel);
            inputContainer.Add(timeLabel);

            var progressLabel = new Label("Iteration: 0 | Loss: 0.000000") {
                style = {
                    fontSize = 12,
                    color = new Color(s_TextColor.r * 0.85f, s_TextColor.g * 0.85f, s_TextColor.b * 0.85f, 1f),
                    marginBottom = 12f,
                    unityTextAlign = TextAnchor.UpperCenter,
                    minWidth = 260f,
                    display = DisplayStyle.None
                }
            };

            var statusLabel = new Label("") {
                style = {
                    fontSize = 12,
                    color = new Color(0.3f, 0.7f, 0.3f, 1f),
                    marginBottom = 12f,
                    unityTextAlign = TextAnchor.UpperCenter,
                    minWidth = 260f,
                    display = DisplayStyle.None
                }
            };

            var button = new Label("Start") {
                style = {
                    paddingTop = 8f, paddingRight = 16f, paddingBottom = 8f, paddingLeft = 16f,
                    backgroundColor = s_BackgroundColor,
                    color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            _targetField.SetValueWithoutNotify(_data.Units.ValueToDisplay(_data.TargetValue));

            _targetField.RegisterValueChangedCallback(evt => {
                _data.TargetValue = _data.Units.DisplayToValue(evt.newValue);
            });

            button.RegisterCallback<MouseEnterEvent>(_ => button.style.backgroundColor = s_HoverColor);
            button.RegisterCallback<MouseLeaveEvent>(_ => button.style.backgroundColor = s_BackgroundColor);
            button.RegisterCallback<MouseDownEvent>(_ => HandleButtonClick());

            panel.Add(_title);
            panel.Add(inputContainer);
            panel.Add(progressLabel);
            panel.Add(statusLabel);
            panel.Add(button);
            Add(panel);

            panel.style.opacity = 0f;
            panel.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            panel.style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            panel.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            RegisterCallback<MouseDownEvent>(evt => {
                if (evt.target == this) {
                    Close();
                    evt.StopPropagation();
                }
            });

            RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Escape) {
                    Close();
                    evt.StopPropagation();
                }
            });

            schedule.Execute(() => { panel.style.opacity = 1f; });

            var progressBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.Loss)),
                bindingMode = BindingMode.ToTarget
            };
            progressBinding.sourceToUiConverters.AddConverter((ref float loss) => {
                return $"Iteration: {_data.Iteration} | Loss: {loss:F2}";
            });

            var progressVisibilityBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.IsStarted)),
                bindingMode = BindingMode.ToTarget
            };
            progressVisibilityBinding.sourceToUiConverters.AddConverter((ref bool isStarted) => {
                return (StyleEnum<DisplayStyle>)(isStarted ? DisplayStyle.Flex : DisplayStyle.None);
            });

            var inputVisibilityBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.IsStarted)),
                bindingMode = BindingMode.ToTarget
            };
            inputVisibilityBinding.sourceToUiConverters.AddConverter((ref bool isStarted) => {
                return (StyleEnum<DisplayStyle>)(isStarted ? DisplayStyle.None : DisplayStyle.Flex);
            });

            var statusDisplayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.IsComplete)),
                bindingMode = BindingMode.ToTarget
            };
            statusDisplayBinding.sourceToUiConverters.AddConverter((ref bool isComplete) => {
                return (StyleEnum<DisplayStyle>)(isComplete ? DisplayStyle.Flex : DisplayStyle.None);
            });

            var statusTextBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.IsSuccessful)),
                bindingMode = BindingMode.ToTarget
            };
            statusTextBinding.sourceToUiConverters.AddConverter((ref bool isSuccessful) => {
                if (!_data.IsComplete) return "";
                return isSuccessful ? "Optimization Complete" : "Optimization Failed";
            });

            var statusColorBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.IsSuccessful)),
                bindingMode = BindingMode.ToTarget
            };
            statusColorBinding.sourceToUiConverters.AddConverter((ref bool isSuccessful) => {
                if (!_data.IsComplete) return (StyleColor)Color.clear;
                return (StyleColor)(isSuccessful ?
                    new Color(0.3f, 0.7f, 0.3f, 1f) : // Green
                    new Color(0.7f, 0.3f, 0.3f, 1f)); // Red
            });

            var buttonBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.IsComplete)),
                bindingMode = BindingMode.ToTarget
            };
            buttonBinding.sourceToUiConverters.AddConverter((ref bool isComplete) => {
                if (isComplete) return "Done";
                return _data.IsStarted ? "Cancel" : "Start";
            });

            var targetValueBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.TargetValue)),
                bindingMode = BindingMode.ToTarget
            };
            targetValueBinding.sourceToUiConverters.AddConverter((ref float value) => {
                return _data.Units.ValueToDisplay(value);
            });

            var unitBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(OptimizerData.Units)),
                bindingMode = BindingMode.ToTarget
            };
            unitBinding.sourceToUiConverters.AddConverter((ref UnitsType value) => value.ToDisplayString());

            progressLabel.SetBinding("text", progressBinding);
            progressLabel.SetBinding("style.display", progressVisibilityBinding);
            inputContainer.SetBinding("style.display", inputVisibilityBinding);
            statusLabel.SetBinding("style.display", statusDisplayBinding);
            statusLabel.SetBinding("text", statusTextBinding);
            statusLabel.SetBinding("style.color", statusColorBinding);
            button.SetBinding("text", buttonBinding);
            _targetField.SetBinding("value", targetValueBinding);
            _unitLabel.SetBinding("text", unitBinding);
        }

        private string FormatTimeString() {
            _sb.Clear();
            _sb.Append("at ");
            _sb.Append(_data.Time.ToString("0.00"));
            _sb.Append(_durationTypeUnits[_data.DurationType]);
            return _sb.ToString();
        }

        private void HandleButtonClick() {
            if (_data.IsStarted || _data.IsComplete) {
                Close();
            }
            else {
                _data.TargetValue = _data.Units.DisplayToValue(_targetField.value);
                _data.IsStarted = true;
                _title.text = "Optimizing";
            }
        }

        private void Close() {
            if (_data.IsStarted && !_data.IsComplete) {
                _data.IsCanceled = true;
            }
            _targetField.Blur();


            schedule.Execute(() => {
                RemoveFromHierarchy();
                _onClose?.Invoke();
            });
        }
    }
}
