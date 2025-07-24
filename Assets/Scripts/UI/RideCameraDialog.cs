using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class RideCameraDialog : VisualElement {
        private Action _onClose;

        private VisualElement _panel;
        private FloatField _rideCameraHeightField;

        public RideCameraDialog(Action onClose) {
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
                    minWidth = 300f
                }
            };

            var title = new Label("Ride Camera") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 12f, unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var fieldContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 16f
                }
            };

            var rideCameraHeightLabel = new Label("Height:") {
                style = { fontSize = 12, color = s_TextColor, marginRight = 8f }
            };

            _rideCameraHeightField = new FloatField {
                value = Preferences.RideCameraHeight,
                isDelayed = true,
                style = { width = 60f }
            };

            _rideCameraHeightField.RegisterValueChangedCallback(evt => {
                Preferences.RideCameraHeight = evt.newValue;
            });

            var buttonContainer = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd }
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

            resetButton.RegisterCallback<MouseDownEvent>(_ => {
                ResetToDefaults();
            });

            closeButton.RegisterCallback<MouseDownEvent>(_ => {
                Close();
            });

            fieldContainer.Add(rideCameraHeightLabel);
            fieldContainer.Add(_rideCameraHeightField);
            buttonContainer.Add(resetButton);
            buttonContainer.Add(closeButton);

            _panel.Add(title);
            _panel.Add(fieldContainer);
            _panel.Add(buttonContainer);
            Add(_panel);

            _panel.style.opacity = 0f;
            _panel.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            _panel.style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            _panel.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Escape) {
                    Close();
                }
                evt.StopPropagation();
            });

            schedule.Execute(() => { _panel.style.opacity = 1f; });
        }

        private void ResetToDefaults() {
            Preferences.RideCameraHeight = DEFAULT_RIDE_CAMERA_HEIGHT;
            _rideCameraHeightField.SetValueWithoutNotify(Preferences.RideCameraHeight);
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
