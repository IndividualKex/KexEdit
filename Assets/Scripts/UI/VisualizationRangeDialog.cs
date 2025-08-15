using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class VisualizationRangeDialog : VisualElement {
        private Action _onClose;
        private VisualElement _panel;
        private List<RangeFieldRow> _rangeRows = new();

        private class RangeFieldRow {
            public VisualizationMode Mode;
            public FloatField MinField;
            public FloatField MaxField;
            public float DefaultMin;
            public float DefaultMax;
        }

        public VisualizationRangeDialog(Action onClose) {
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

            _panel = new VisualElement {
                style = {
                    backgroundColor = s_BackgroundColor,
                    borderTopLeftRadius = 3f, borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f,
                    borderTopWidth = 1f, borderRightWidth = 1f, borderBottomWidth = 1f, borderLeftWidth = 1f,
                    borderTopColor = s_BorderColor, borderRightColor = s_BorderColor,
                    borderBottomColor = s_BorderColor, borderLeftColor = s_BorderColor,
                    paddingTop = 16f, paddingRight = 16f, paddingBottom = 16f, paddingLeft = 16f,
                    minWidth = 400f
                }
            };

            CreateUI();

            _panel.style.opacity = 0f;
            _panel.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            _panel.style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            _panel.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            RegisterCallback<MouseDownEvent>(evt => {
                if (evt.target == this) {
                    Close();
                    evt.StopPropagation();
                }
            });

            RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Escape) {
                    Close();
                }
                evt.StopPropagation();
            });

            schedule.Execute(() => { _panel.style.opacity = 1f; });
        }

        private void CreateUI() {
            var title = new Label("Visualization Ranges") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 12f, unityTextAlign = TextAnchor.UpperCenter
                }
            };
            _panel.Add(title);

            CreateRangeFields();
            CreateButtons();
            Add(_panel);
        }

        private void CreateRangeFields() {
            var modes = new[] {
                (VisualizationMode.Velocity, $"Velocity {Units.GetSpeedUnitsSuffix()}", 0f, 50f),
                (VisualizationMode.NormalForce, "Normal Force (G)", -2f, 5f),
                (VisualizationMode.LateralForce, "Lateral Force (G)", -2f, 2f),
                (VisualizationMode.RollSpeed, $"Roll Speed {Units.GetAnglePerTimeSuffix()}", -3f, 3f),
                (VisualizationMode.PitchSpeed, $"Pitch Speed {Units.GetAnglePerTimeSuffix()}", -1f, 1f),
                (VisualizationMode.YawSpeed, $"Yaw Speed {Units.GetAnglePerTimeSuffix()}", -1f, 1f),
                (VisualizationMode.Curvature, $"Curvature {Units.GetAnglePerTimeSuffix()}", 0f, 1f)
            };

            foreach (var (mode, label, defaultMin, defaultMax) in modes) {
                CreateRangeField(mode, label, defaultMin, defaultMax);
            }
        }

        private void CreateRangeField(VisualizationMode mode, string label, float defaultMin, float defaultMax) {
            var fieldContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f
                }
            };

            var modeLabel = new Label(label) {
                style = {
                    fontSize = 12,
                    color = s_TextColor,
                    marginRight = 8f,
                    width = 140f
                }
            };

            var currentRange = Preferences.GetVisualizationRange(mode);
            float displayMin = ConvertValueToDisplay(mode, currentRange.x);
            float displayMax = ConvertValueToDisplay(mode, currentRange.y);
            float displayDefaultMin = ConvertValueToDisplay(mode, defaultMin);
            float displayDefaultMax = ConvertValueToDisplay(mode, defaultMax);

            var minField = new FloatField {
                value = displayMin,
                isDelayed = true,
                style = { width = 60f, marginRight = 4f }
            };

            var toLabel = new Label("to") {
                style = {
                    fontSize = 12,
                    color = s_TextColor,
                    marginLeft = 4f,
                    marginRight = 4f
                }
            };

            var maxField = new FloatField {
                value = displayMax,
                isDelayed = true,
                style = { width = 60f }
            };

            var rangeRow = new RangeFieldRow {
                Mode = mode,
                MinField = minField,
                MaxField = maxField,
                DefaultMin = displayDefaultMin,
                DefaultMax = displayDefaultMax
            };

            _rangeRows.Add(rangeRow);

            minField.RegisterValueChangedCallback(evt => OnRangeFieldChanged(rangeRow));
            maxField.RegisterValueChangedCallback(evt => OnRangeFieldChanged(rangeRow));

            fieldContainer.Add(modeLabel);
            fieldContainer.Add(minField);
            fieldContainer.Add(toLabel);
            fieldContainer.Add(maxField);

            _panel.Add(fieldContainer);
        }

        private void CreateButtons() {
            var buttonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop = 16f
                }
            };

            var resetButton = new Label("Reset to Defaults") {
                style = {
                    marginRight = 8f,
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            var closeButton = new Label("Close") {
                style = {
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            resetButton.RegisterCallback<MouseEnterEvent>(_ => resetButton.style.backgroundColor = s_HoverColor);
            resetButton.RegisterCallback<MouseLeaveEvent>(_ => resetButton.style.backgroundColor = s_BackgroundColor);
            closeButton.RegisterCallback<MouseEnterEvent>(_ => closeButton.style.backgroundColor = s_HoverColor);
            closeButton.RegisterCallback<MouseLeaveEvent>(_ => closeButton.style.backgroundColor = s_BackgroundColor);

            resetButton.RegisterCallback<MouseDownEvent>(_ => ResetToDefaults());
            closeButton.RegisterCallback<MouseDownEvent>(_ => Close());

            buttonContainer.Add(resetButton);
            buttonContainer.Add(closeButton);
            _panel.Add(buttonContainer);
        }

        private void OnRangeFieldChanged(RangeFieldRow rangeRow) {
            float displayMin = rangeRow.MinField.value;
            float displayMax = rangeRow.MaxField.value;

            if (displayMax <= displayMin) {
                displayMax = displayMin + 0.1f;
                rangeRow.MaxField.SetValueWithoutNotify(displayMax);
            }

            float internalMin = ConvertDisplayToValue(rangeRow.Mode, displayMin);
            float internalMax = ConvertDisplayToValue(rangeRow.Mode, displayMax);
            Preferences.SetVisualizationRange(rangeRow.Mode, internalMin, internalMax);
        }

        private void ResetToDefaults() {
            Preferences.ResetVisualizationRanges();

            foreach (var rangeRow in _rangeRows) {
                rangeRow.MinField.SetValueWithoutNotify(rangeRow.DefaultMin);
                rangeRow.MaxField.SetValueWithoutNotify(rangeRow.DefaultMax);
            }
        }

        private void Close() {
            foreach (var rangeRow in _rangeRows) {
                rangeRow.MinField.Blur();
                rangeRow.MaxField.Blur();
            }

            schedule.Execute(() => {
                RemoveFromHierarchy();
                _onClose?.Invoke();
            });
        }

        private float ConvertValueToDisplay(VisualizationMode mode, float value) {
            return mode switch {
                VisualizationMode.Velocity => Units.SpeedToDisplay(value),
                VisualizationMode.RollSpeed => Units.AnglePerTimeToDisplay(value),
                VisualizationMode.PitchSpeed => Units.AnglePerTimeToDisplay(value),
                VisualizationMode.YawSpeed => Units.AnglePerTimeToDisplay(value),
                VisualizationMode.Curvature => Units.AnglePerTimeToDisplay(value),
                _ => value
            };
        }

        private float ConvertDisplayToValue(VisualizationMode mode, float displayValue) {
            return mode switch {
                VisualizationMode.Velocity => Units.DisplayToSpeed(displayValue),
                VisualizationMode.RollSpeed => Units.DisplayToAnglePerTime(displayValue),
                VisualizationMode.PitchSpeed => Units.DisplayToAnglePerTime(displayValue),
                VisualizationMode.YawSpeed => Units.DisplayToAnglePerTime(displayValue),
                VisualizationMode.Curvature => Units.DisplayToAnglePerTime(displayValue),
                _ => displayValue
            };
        }
    }
}
