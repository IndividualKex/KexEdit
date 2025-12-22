using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class SensitivityDialog : VisualElement {
        private Action _onClose;
        private VisualElement _panel;
        private FloatField _scrollSensitivityField;
        private FloatField _pointerSensitivityField;

        public SensitivityDialog(Action onClose) {
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

            var title = new Label("Input Sensitivity") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 12f, unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var scrollContainer = new VisualElement {
                style = { flexDirection = FlexDirection.Row, marginBottom = 8f, alignItems = Align.Center }
            };
            var scrollLabel = new Label("Scroll Sensitivity:") {
                style = { color = s_TextColor, width = 120f }
            };
            _scrollSensitivityField = new FloatField {
                value = Preferences.ScrollSensitivity,
                style = { flexGrow = 1f }
            };
            _scrollSensitivityField.RegisterValueChangedCallback(evt => {
                float clampedValue = Mathf.Clamp(evt.newValue, 0.1f, 3f);
                if (Mathf.Abs(evt.newValue - clampedValue) > 0.001f) {
                    _scrollSensitivityField.SetValueWithoutNotify(clampedValue);
                }
                Preferences.ScrollSensitivity = clampedValue;
            });
            scrollContainer.Add(scrollLabel);
            scrollContainer.Add(_scrollSensitivityField);

            var pointerContainer = new VisualElement {
                style = { flexDirection = FlexDirection.Row, marginBottom = 16f, alignItems = Align.Center }
            };
            var pointerLabel = new Label("Pointer Sensitivity:") {
                style = { color = s_TextColor, width = 120f }
            };
            _pointerSensitivityField = new FloatField {
                value = Preferences.PointerSensitivity,
                style = { flexGrow = 1f }
            };
            _pointerSensitivityField.RegisterValueChangedCallback(evt => {
                float clampedValue = Mathf.Clamp(evt.newValue, 0.1f, 3f);
                if (Mathf.Abs(evt.newValue - clampedValue) > 0.001f) {
                    _pointerSensitivityField.SetValueWithoutNotify(clampedValue);
                }
                Preferences.PointerSensitivity = clampedValue;
            });
            pointerContainer.Add(pointerLabel);
            pointerContainer.Add(_pointerSensitivityField);

            var rangeHint = new Label("Range: 0.1 to 3.0") {
                style = {
                    color = new Color(0.7f, 0.7f, 0.7f),
                    fontSize = 10,
                    marginBottom = 16f,
                    unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var buttonContainer = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center }
            };

            var closeButton = new Label("Close") {
                style = {
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            closeButton.RegisterCallback<MouseEnterEvent>(_ => closeButton.style.backgroundColor = s_HoverColor);
            closeButton.RegisterCallback<MouseLeaveEvent>(_ => closeButton.style.backgroundColor = s_BackgroundColor);

            closeButton.RegisterCallback<MouseDownEvent>(_ => {
                Close();
            });

            buttonContainer.Add(closeButton);

            _panel.Add(title);
            _panel.Add(scrollContainer);
            _panel.Add(pointerContainer);
            _panel.Add(rangeHint);
            _panel.Add(buttonContainer);
            Add(_panel);

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
                    evt.StopPropagation();
                }
            });

            schedule.Execute(() => { _panel.style.opacity = 1f; });
        }

        private void Close() {
            _scrollSensitivityField.Blur();
            _pointerSensitivityField.Blur();

            schedule.Execute(() => {
                RemoveFromHierarchy();
                _onClose?.Invoke();
            });
        }
    }
}
