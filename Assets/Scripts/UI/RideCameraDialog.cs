using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.Constants;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class RideCameraDialog : VisualElement {
        private Action _onClose;

        private VisualElement _panel;
        private FloatField _positionXField;
        private FloatField _positionYField;
        private FloatField _positionZField;
        private FloatField _rotationXField;
        private FloatField _rotationYField;
        private FloatField _rotationZField;

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
                style = { marginBottom = 16f }
            };

            var positionLabel = new Label("Position:") {
                style = { fontSize = 12, color = s_TextColor, marginBottom = 8f }
            };

            var positionContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 12f
                }
            };

            var positionXLabel = new Label("X:") {
                style = { fontSize = 11, color = s_TextColor, marginRight = 4f }
            };

            _positionXField = new FloatField {
                value = Preferences.RideCameraPositionX,
                isDelayed = true,
                style = { width = 50f, marginRight = 8f }
            };

            _positionXField.RegisterValueChangedCallback(evt => {
                Preferences.RideCameraPositionX = evt.newValue;
            });

            var positionYLabel = new Label("Y:") {
                style = { fontSize = 11, color = s_TextColor, marginRight = 4f }
            };

            _positionYField = new FloatField {
                value = Preferences.RideCameraPositionY,
                isDelayed = true,
                style = { width = 50f, marginRight = 8f }
            };

            _positionYField.RegisterValueChangedCallback(evt => {
                Preferences.RideCameraPositionY = evt.newValue;
            });

            var positionZLabel = new Label("Z:") {
                style = { fontSize = 11, color = s_TextColor, marginRight = 4f }
            };

            _positionZField = new FloatField {
                value = Preferences.RideCameraPositionZ,
                isDelayed = true,
                style = { width = 50f }
            };

            _positionZField.RegisterValueChangedCallback(evt => {
                Preferences.RideCameraPositionZ = evt.newValue;
            });

            var rotationLabel = new Label("Rotation:") {
                style = { fontSize = 12, color = s_TextColor, marginBottom = 8f }
            };

            var rotationContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 12f
                }
            };

            var rotationXLabel = new Label("X:") {
                style = { fontSize = 11, color = s_TextColor, marginRight = 4f }
            };

            _rotationXField = new FloatField {
                value = Preferences.RideCameraRotationX,
                isDelayed = true,
                style = { width = 50f, marginRight = 8f }
            };

            _rotationXField.RegisterValueChangedCallback(evt => {
                Preferences.RideCameraRotationX = evt.newValue;
            });

            var rotationYLabel = new Label("Y:") {
                style = { fontSize = 11, color = s_TextColor, marginRight = 4f }
            };

            _rotationYField = new FloatField {
                value = Preferences.RideCameraRotationY,
                isDelayed = true,
                style = { width = 50f, marginRight = 8f }
            };

            _rotationYField.RegisterValueChangedCallback(evt => {
                Preferences.RideCameraRotationY = evt.newValue;
            });

            var rotationZLabel = new Label("Z:") {
                style = { fontSize = 11, color = s_TextColor, marginRight = 4f }
            };

            _rotationZField = new FloatField {
                value = Preferences.RideCameraRotationZ,
                isDelayed = true,
                style = { width = 50f }
            };

            _rotationZField.RegisterValueChangedCallback(evt => {
                Preferences.RideCameraRotationZ = evt.newValue;
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

            positionContainer.Add(positionXLabel);
            positionContainer.Add(_positionXField);
            positionContainer.Add(positionYLabel);
            positionContainer.Add(_positionYField);
            positionContainer.Add(positionZLabel);
            positionContainer.Add(_positionZField);

            rotationContainer.Add(rotationXLabel);
            rotationContainer.Add(_rotationXField);
            rotationContainer.Add(rotationYLabel);
            rotationContainer.Add(_rotationYField);
            rotationContainer.Add(rotationZLabel);
            rotationContainer.Add(_rotationZField);

            fieldContainer.Add(positionLabel);
            fieldContainer.Add(positionContainer);
            fieldContainer.Add(rotationLabel);
            fieldContainer.Add(rotationContainer);
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

        private void ResetToDefaults() {
            Preferences.RideCameraPositionX = DEFAULT_RIDE_CAMERA_POSITION_X;
            Preferences.RideCameraPositionY = DEFAULT_RIDE_CAMERA_POSITION_Y;
            Preferences.RideCameraPositionZ = DEFAULT_RIDE_CAMERA_POSITION_Z;
            Preferences.RideCameraRotationX = DEFAULT_RIDE_CAMERA_ROTATION_X;
            Preferences.RideCameraRotationY = DEFAULT_RIDE_CAMERA_ROTATION_Y;
            Preferences.RideCameraRotationZ = DEFAULT_RIDE_CAMERA_ROTATION_Z;

            _positionXField.SetValueWithoutNotify(Preferences.RideCameraPositionX);
            _positionYField.SetValueWithoutNotify(Preferences.RideCameraPositionY);
            _positionZField.SetValueWithoutNotify(Preferences.RideCameraPositionZ);
            _rotationXField.SetValueWithoutNotify(Preferences.RideCameraRotationX);
            _rotationYField.SetValueWithoutNotify(Preferences.RideCameraRotationY);
            _rotationZField.SetValueWithoutNotify(Preferences.RideCameraRotationZ);
        }

        private void Close() {
            _positionXField.Blur();
            _positionYField.Blur();
            _positionZField.Blur();
            _rotationXField.Blur();
            _rotationYField.Blur();
            _rotationZField.Blur();

            schedule.Execute(() => {
                RemoveFromHierarchy();
                _onClose?.Invoke();
            });
        }
    }
}
