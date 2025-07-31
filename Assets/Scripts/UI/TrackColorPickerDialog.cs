using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class TrackColorPickerDialog : VisualElement {
        private Action _onClose;
        private VisualElement _panel;
        private string _currentTrackStyle;
        private TrackStyleConfig _trackConfig;
        private List<ColorFieldRow> _colorRows = new();

        private class ColorFieldRow {
            public int Index;
            public FloatField RField;
            public FloatField GField;
            public FloatField BField;
            public VisualElement ColorPreview;
            public Color DefaultColor;
        }

        public TrackColorPickerDialog(Action onClose) {
            _onClose = onClose;
            _currentTrackStyle = Preferences.CurrentTrackStyle;

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

            LoadTrackConfig();
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

        private void LoadTrackConfig() {
            _trackConfig = TrackStyleResourceLoader.LoadConfig(_currentTrackStyle);
        }

        private string GetTrackDisplayName() {
            if (!string.IsNullOrEmpty(_trackConfig?.Name)) {
                return _trackConfig.Name;
            }
            return _currentTrackStyle.Replace(".json", "");
        }

        private void CreateUI() {
            var title = new Label($"Track Colors - {GetTrackDisplayName()}") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 12f, unityTextAlign = TextAnchor.UpperCenter
                }
            };
            _panel.Add(title);

            if (_trackConfig?.Colors == null || _trackConfig.Colors.Length == 0) {
                var noColorsLabel = new Label("This track style has no customizable colors.") {
                    style = {
                        color = s_TextColor,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        marginBottom = 16f
                    }
                };
                _panel.Add(noColorsLabel);
            }
            else {
                CreateColorFields();
            }

            CreateButtons();
            Add(_panel);
        }

        private void CreateColorFields() {
            for (int i = 0; i < _trackConfig.Colors.Length; i++) {
                CreateColorField(i, _trackConfig.Colors[i]);
            }
        }

        private void CreateColorField(int colorIndex, Color defaultColor) {
            var fieldContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f
                }
            };

            var colorLabel = new Label($"Color {colorIndex}:") {
                style = {
                    fontSize = 12,
                    color = s_TextColor,
                    marginRight = 8f,
                    width = 60f
                }
            };

            var currentColor = TrackColorPreferences.GetColor(_currentTrackStyle, colorIndex, defaultColor);

            var rField = new FloatField {
                value = currentColor.r,
                isDelayed = true,
                style = { width = 50f, marginRight = 4f }
            };

            var gField = new FloatField {
                value = currentColor.g,
                isDelayed = true,
                style = { width = 50f, marginRight = 4f }
            };

            var bField = new FloatField {
                value = currentColor.b,
                isDelayed = true,
                style = { width = 50f, marginRight = 8f }
            };

            var colorPreview = new VisualElement {
                style = {
                    width = 30f,
                    height = 20f,
                    backgroundColor = currentColor,
                    borderTopWidth = 1f, borderRightWidth = 1f, borderBottomWidth = 1f, borderLeftWidth = 1f,
                    borderTopColor = s_BorderColor, borderRightColor = s_BorderColor,
                    borderBottomColor = s_BorderColor, borderLeftColor = s_BorderColor
                }
            };

            var colorRow = new ColorFieldRow {
                Index = colorIndex,
                RField = rField,
                GField = gField,
                BField = bField,
                ColorPreview = colorPreview,
                DefaultColor = defaultColor
            };

            _colorRows.Add(colorRow);

            rField.RegisterValueChangedCallback(evt => OnColorFieldChanged(colorRow));
            gField.RegisterValueChangedCallback(evt => OnColorFieldChanged(colorRow));
            bField.RegisterValueChangedCallback(evt => OnColorFieldChanged(colorRow));

            fieldContainer.Add(colorLabel);
            fieldContainer.Add(new Label("R") { style = { fontSize = 10, color = s_TextColor, marginRight = 2f } });
            fieldContainer.Add(rField);
            fieldContainer.Add(new Label("G") { style = { fontSize = 10, color = s_TextColor, marginRight = 2f } });
            fieldContainer.Add(gField);
            fieldContainer.Add(new Label("B") { style = { fontSize = 10, color = s_TextColor, marginRight = 2f } });
            fieldContainer.Add(bField);
            fieldContainer.Add(colorPreview);

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

            var resetButton = new Label("Reset to Default") {
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

        private void OnColorFieldChanged(ColorFieldRow colorRow) {
            float r = Mathf.Clamp01(colorRow.RField.value);
            float g = Mathf.Clamp01(colorRow.GField.value);
            float b = Mathf.Clamp01(colorRow.BField.value);

            var newColor = new Color(r, g, b, 1f);
            colorRow.ColorPreview.style.backgroundColor = newColor;
            TrackColorPreferences.SetColor(_currentTrackStyle, colorRow.Index, newColor);

            colorRow.RField.SetValueWithoutNotify(r);
            colorRow.GField.SetValueWithoutNotify(g);
            colorRow.BField.SetValueWithoutNotify(b);

            TriggerTrackStyleReload();
        }

        private void TriggerTrackStyleReload() {
            try {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                var loadEntity = ecb.CreateEntity();
                ecb.AddComponent<ReloadTrackStyleEvent>(loadEntity);
                ecb.Playback(entityManager);
            }
            catch (System.Exception ex) {
                Debug.LogError($"Failed to trigger track style reload: {ex.Message}");
            }
        }

        private void ResetToDefaults() {
            TrackColorPreferences.ResetAllColors(_currentTrackStyle);

            foreach (var colorRow in _colorRows) {
                var defaultColor = colorRow.DefaultColor;
                colorRow.RField.SetValueWithoutNotify(defaultColor.r);
                colorRow.GField.SetValueWithoutNotify(defaultColor.g);
                colorRow.BField.SetValueWithoutNotify(defaultColor.b);
                colorRow.ColorPreview.style.backgroundColor = defaultColor;
            }

            TriggerTrackStyleReload();
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
