using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class ExportDialog : VisualElement {
        private Action<float> _onExport;
        private Action _onClose;

        private VisualElement _panel;
        private FloatField _metersField;

        public ExportDialog(Action<float> onExport, Action onClose) {
            _onExport = onExport;
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

            var title = new Label("Export to NoLimits 2") {
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

            var metersLabel = new Label("Meters per Node:") {
                style = { fontSize = 12, color = s_TextColor, marginRight = 8f }
            };

            _metersField = new FloatField {
                value = 2f,
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

            var exportButton = new Label("Export") {
                style = {
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            cancelButton.RegisterCallback<MouseEnterEvent>(_ => cancelButton.style.backgroundColor = s_HoverColor);
            cancelButton.RegisterCallback<MouseLeaveEvent>(_ => cancelButton.style.backgroundColor = s_BackgroundColor);
            exportButton.RegisterCallback<MouseEnterEvent>(_ => exportButton.style.backgroundColor = s_HoverColor);
            exportButton.RegisterCallback<MouseLeaveEvent>(_ => exportButton.style.backgroundColor = s_BackgroundColor);

            cancelButton.RegisterCallback<MouseDownEvent>(_ => {
                Close();
            });

            exportButton.RegisterCallback<MouseDownEvent>(_ => {
                Export();
            });

            fieldContainer.Add(metersLabel);
            fieldContainer.Add(_metersField);
            buttonContainer.Add(cancelButton);
            buttonContainer.Add(exportButton);

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
                    Export();
                }
                else if (evt.keyCode == KeyCode.Escape) {
                    Close();
                }
                evt.StopPropagation();
            });

            schedule.Execute(() => { _panel.style.opacity = 1f; });
        }

        private void Export() {
            float value = _metersField.value;
            if (value <= 0f) return;
            _onExport?.Invoke(value);
            Close();
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
