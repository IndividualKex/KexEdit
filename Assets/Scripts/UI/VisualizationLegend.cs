using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using static KexEdit.Constants;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class VisualizationLegend : VisualElement {
        private VisualizationLegendData _data;
        private VisualElement _gradientBar;
        private Label _titleLabel;
        private Label _minLabel;
        private Label _maxLabel;
        private Label _unitsLabel;

        public VisualizationLegend(VisualizationLegendData data) {
            _data = data;
            dataSource = _data;
            pickingMode = PickingMode.Ignore;

            style.position = Position.Absolute;
            style.top = 16f;
            style.left = 16f;
            style.width = 200f;
            style.backgroundColor = new Color(0, 0, 0, 0.5f);
            style.borderTopLeftRadius = 4f;
            style.borderTopRightRadius = 4f;
            style.borderBottomLeftRadius = 4f;
            style.borderBottomRightRadius = 4f;
            style.paddingTop = 8f;
            style.paddingRight = 12f;
            style.paddingBottom = 8f;
            style.paddingLeft = 12f;
            style.opacity = 0f;
            style.transitionProperty = new List<StylePropertyName> { "opacity" };
            style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            _titleLabel = new Label {
                style = {
                    color = s_ActiveTextColor,
                    marginBottom = 6f,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            _gradientBar = new VisualElement {
                style = {
                    height = 8f,
                    marginBottom = 6f,
                    borderTopLeftRadius = 2f,
                    borderTopRightRadius = 2f,
                    borderBottomLeftRadius = 2f,
                    borderBottomRightRadius = 2f
                }
            };
            _gradientBar.generateVisualContent += OnGenerateGradient;

            var valuesContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween
                }
            };

            _minLabel = new Label {
                style = {
                    fontSize = 10,
                    color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            _maxLabel = new Label {
                style = {
                    fontSize = 10,
                    color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleRight
                }
            };

            _unitsLabel = new Label {
                style = {
                    fontSize = 10,
                    color = s_MutedTextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            valuesContainer.Add(_minLabel);
            valuesContainer.Add(_unitsLabel);
            valuesContainer.Add(_maxLabel);

            Add(_titleLabel);
            Add(_gradientBar);
            Add(valuesContainer);

            var titleBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.VisualizationName)),
                bindingMode = BindingMode.ToTarget,
            };
            _titleLabel.SetBinding("text", titleBinding);

            var visibilityBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.IsVisible)),
                bindingMode = BindingMode.ToTarget,
            };
            visibilityBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                (StyleFloat)(value ? 1f : 0f));
            SetBinding("style.opacity", visibilityBinding);

            var minBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.MinValue)),
                bindingMode = BindingMode.ToTarget,
            };
            minBinding.sourceToUiConverters.AddConverter((ref float value) => {
                _gradientBar.MarkDirtyRepaint();
                return FormatValueOnly(value);
            });
            _minLabel.SetBinding("text", minBinding);

            var maxBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.MaxValue)),
                bindingMode = BindingMode.ToTarget,
            };
            maxBinding.sourceToUiConverters.AddConverter((ref float value) => {
                _gradientBar.MarkDirtyRepaint();
                return FormatValueOnly(value);
            });
            _maxLabel.SetBinding("text", maxBinding);

            var unitsBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.UnitsString)),
                bindingMode = BindingMode.ToTarget,
            };
            _unitsLabel.SetBinding("text", unitsBinding);

            var gradientTypeBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.GradientType)),
                bindingMode = BindingMode.ToTarget,
            };
            gradientTypeBinding.sourceToUiConverters.AddConverter((ref VisualizationGradientType value) => {
                _gradientBar.MarkDirtyRepaint();
                return value;
            });
            SetBinding("GradientType", gradientTypeBinding);
        }

        private void OnGenerateGradient(MeshGenerationContext ctx) {
            var painter = ctx.painter2D;
            var rect = _gradientBar.contentRect;

            const int gradientSteps = 64;
            float stepWidth = rect.width / gradientSteps;

            for (int i = 0; i < gradientSteps; i++) {
                float t = i / (float)(gradientSteps - 1);
                Color color;

                if (_data.GradientType == VisualizationGradientType.TwoColorPositive) {
                    color = Color.Lerp(VISUALIZATION_ZERO_COLOR, VISUALIZATION_MAX_COLOR, t);
                } else {
                    float minValue = _data.MinValue;
                    float maxValue = _data.MaxValue;
                    float value = Mathf.Lerp(minValue, maxValue, t);
                    float neutralPoint = _data.NeutralOffset;
                    
                    if (value < neutralPoint) {
                        float adjustedMin = minValue - neutralPoint;
                        float adjustedValue = value - neutralPoint;
                        float negativeT = Mathf.Clamp01(adjustedValue / adjustedMin);
                        color = Color.Lerp(VISUALIZATION_ZERO_COLOR, VISUALIZATION_MIN_COLOR, negativeT);
                    } else {
                        float adjustedMax = maxValue - neutralPoint;
                        float adjustedValue = value - neutralPoint;
                        float positiveT = Mathf.Clamp01(adjustedValue / adjustedMax);
                        color = Color.Lerp(VISUALIZATION_ZERO_COLOR, VISUALIZATION_MAX_COLOR, positiveT);
                    }
                }

                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(i * stepWidth, 0));
                painter.LineTo(new Vector2((i + 1) * stepWidth, 0));
                painter.LineTo(new Vector2((i + 1) * stepWidth, rect.height));
                painter.LineTo(new Vector2(i * stepWidth, rect.height));
                painter.Fill();
            }
        }

        private string FormatValueOnly(float value) {
            return value switch {
                _ when Mathf.Abs(value) < 0.001f => "0",
                _ when Mathf.Abs(value) >= 1000f => value.ToString("F0"),
                _ when Mathf.Abs(value) >= 100f => value.ToString("F1"),
                _ => value.ToString("F2")
            };
        }
    }
}
