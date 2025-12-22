using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public class UnsavedChangesDialog : VisualElement {
        private Action _onSave;
        private Action _onDontSave;
        private Action _onClose;

        public UnsavedChangesDialog(Action onSave, Action onDontSave, Action onClose) {
            _onSave = onSave;
            _onDontSave = onDontSave;
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

            var panel = new VisualElement {
                style = {
                    backgroundColor = s_BackgroundColor,
                    borderTopLeftRadius = 3f, borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f,
                    borderTopWidth = 1f, borderRightWidth = 1f, borderBottomWidth = 1f, borderLeftWidth = 1f,
                    borderTopColor = s_BorderColor, borderRightColor = s_BorderColor,
                    borderBottomColor = s_BorderColor, borderLeftColor = s_BorderColor,
                    paddingTop = 16f, paddingRight = 16f, paddingBottom = 16f, paddingLeft = 16f,
                    minWidth = 320f
                }
            };

            var title = new Label("Save changes before continuing?") {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 8f, unityTextAlign = TextAnchor.UpperLeft
                }
            };

            var message = new Label("Your changes will be lost if you don't save them.") {
                style = {
                    fontSize = 12, color = new Color(s_TextColor.r * 0.8f, s_TextColor.g * 0.8f, s_TextColor.b * 0.8f, 1f),
                    whiteSpace = WhiteSpace.Normal, marginBottom = 16f
                }
            };

            var buttonContainer = new VisualElement {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 8f }
            };

            var dontSaveButton = new Label("Don't Save") {
                style = {
                    marginRight = 8f,
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            var cancelButton = new Label("Cancel") {
                style = {
                    marginRight = 8f,
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            var saveButton = new Label("Save") {
                style = {
                    paddingTop = 8f, paddingRight = 12f, paddingBottom = 8f, paddingLeft = 12f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            dontSaveButton.RegisterCallback<MouseEnterEvent>(_ => dontSaveButton.style.backgroundColor = s_HoverColor);
            dontSaveButton.RegisterCallback<MouseLeaveEvent>(_ => dontSaveButton.style.backgroundColor = s_BackgroundColor);
            cancelButton.RegisterCallback<MouseEnterEvent>(_ => cancelButton.style.backgroundColor = s_HoverColor);
            cancelButton.RegisterCallback<MouseLeaveEvent>(_ => cancelButton.style.backgroundColor = s_BackgroundColor);
            saveButton.RegisterCallback<MouseEnterEvent>(_ => saveButton.style.backgroundColor = s_HoverColor);
            saveButton.RegisterCallback<MouseLeaveEvent>(_ => saveButton.style.backgroundColor = s_BackgroundColor);

            dontSaveButton.RegisterCallback<MouseDownEvent>(_ => {
                DontSave();
            });

            cancelButton.RegisterCallback<MouseDownEvent>(_ => {
                Close();
            });

            saveButton.RegisterCallback<MouseDownEvent>(_ => {
                Save();
            });

            buttonContainer.Add(dontSaveButton);
            buttonContainer.Add(cancelButton);
            buttonContainer.Add(saveButton);

            panel.Add(title);
            panel.Add(message);
            panel.Add(buttonContainer);
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

            panel.schedule.Execute(() => panel.style.opacity = 1f);
        }

        private void Save() {
            _onSave?.Invoke();
            Close();
        }

        private void DontSave() {
            _onDontSave?.Invoke();
            Close();
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
