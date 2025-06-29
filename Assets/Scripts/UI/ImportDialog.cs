using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class ImportDialog : VisualElement {
        private Action<float, float, float, float> _onImport;
        private Action _onClose;

        private VisualElement _panel;
        private FloatField _scaleField;
        private FloatField _positionXField;
        private FloatField _positionYField;
        private FloatField _positionZField;

        public ImportDialog(Action<float, float, float, float> onImport, Action onClose) {
            _onImport = onImport;
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
                    minWidth = 280f
                }
            };

            var title = new Label("Import from glTF") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 12f, unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var fieldContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.Center,
                    marginBottom = 16f
                }
            };

            var scaleLabel = new Label("Scale:") {
                style = { fontSize = 12, color = s_TextColor }
            };
            _scaleField = new FloatField {
                value = 1f,
                isDelayed = true,
                style = { width = 60f }
            };

            var positionXLabel = new Label("Position X:") {
                style = { fontSize = 12, color = s_TextColor }
            };
            _positionXField = new FloatField {
                value = 1f,
                      isDelayed = true,
                      style = { width = 60f }
            };

            var positionYLabel = new Label("Position Y:") {
                style = { fontSize = 12, color = s_TextColor }
            };
            _positionYField = new FloatField {
                value = 0f,
                      isDelayed = true,
                      style = { width = 60f }
            };

            var positionZLabel = new Label("Position Z:") {
                style = { fontSize = 12, color = s_TextColor }
            };
            _positionZField = new FloatField {
                value = 0f,
                      isDelayed = true,
                      style = { width = 60f }
            };

            var buttonContainer = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd }
            };

            var cancelButton = new Label("Cancel") {
                style = {
                    marginRight = 8f,
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            var importButton = new Label("Import") {
                style = {
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            cancelButton.RegisterCallback<MouseEnterEvent>(_ => cancelButton.style.backgroundColor = s_HoverColor);
            cancelButton.RegisterCallback<MouseLeaveEvent>(_ => cancelButton.style.backgroundColor = s_BackgroundColor);
            importButton.RegisterCallback<MouseEnterEvent>(_ => importButton.style.backgroundColor = s_HoverColor);
            importButton.RegisterCallback<MouseLeaveEvent>(_ => importButton.style.backgroundColor = s_BackgroundColor);

            cancelButton.RegisterCallback<MouseDownEvent>(_ => {
                Close();
            });

            importButton.RegisterCallback<MouseDownEvent>(_ => {
                Import();
            });

            fieldContainer.Add(CreateFieldRow(scaleLabel, _scaleField));
            fieldContainer.Add(CreateFieldRow(positionXLabel, _positionXField));
            fieldContainer.Add(CreateFieldRow(positionYLabel, _positionYField));
            fieldContainer.Add(CreateFieldRow(positionZLabel, _positionZField));
            buttonContainer.Add(cancelButton);
            buttonContainer.Add(importButton);

            _panel.Add(title);
            _panel.Add(fieldContainer);
            _panel.Add(buttonContainer);
            Add(_panel);

            _panel.style.opacity = 0f;
            _panel.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            _panel.style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            _panel.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) {
                    Import();
                }
                else if (evt.keyCode == KeyCode.Escape) {
                    Close();
                }
                evt.StopPropagation();
            });

            schedule.Execute(() => { _panel.style.opacity = 1f; });
        }

        private VisualElement CreateFieldRow(Label label, FloatField field) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f
                }
            };
            label.style.marginRight = 8f;
            row.Add(label);
            row.Add(field);
            return row;
        }

        private void Import() {
            float scale = _scaleField.value;
            float posX = _positionXField.value;
            float posY = _positionYField.value;
            float posZ = _positionZField.value;

            if (scale <= 0f) return;
            _onImport?.Invoke(scale, posX, posY, posZ);
            Close();
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
