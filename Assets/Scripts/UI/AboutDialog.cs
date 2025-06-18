using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class AboutDialog : VisualElement {
        private Action _onClose;
        private VisualElement _panel;

        public AboutDialog(Action onClose) {
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
                    paddingTop = 24f, paddingRight = 24f, paddingBottom = 24f, paddingLeft = 24f,
                    minWidth = 320f,
                    alignItems = Align.Center
                }
            };

            var title = new Label($"{Application.productName}") {
                style = {
                    fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 20f, unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var version = new Label($"Version {Application.version}") {
                style = {
                    fontSize = 12,
                    color = new Color(s_TextColor.r * 0.85f, s_TextColor.g * 0.85f, s_TextColor.b * 0.85f, 1f),
                    marginBottom = 16f, unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var linkContainer = new VisualElement {
                style = {
                    alignItems = Align.Center,
                    marginBottom = 20f
                }
            };

            var linkLabel = new Label("Visit Project Page") {
                style = {
                    fontSize = 11,
                    color = new Color(0.4f, 0.8f, 1f, 1f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 4f, paddingRight = 8f, paddingBottom = 4f, paddingLeft = 8f
                }
            };

            var linkUrl = "https://individualkex.itch.io/kexedit";

            linkLabel.RegisterCallback<MouseEnterEvent>(_ => {
                linkLabel.style.color = new Color(0.6f, 0.9f, 1f, 1f);
            });

            linkLabel.RegisterCallback<MouseLeaveEvent>(_ => {
                linkLabel.style.color = new Color(0.4f, 0.8f, 1f, 1f);
            });

            linkLabel.RegisterCallback<MouseDownEvent>(_ => {
                Application.OpenURL(linkUrl);
            });

            linkContainer.Add(linkLabel);

            var closeButton = new Label("Close") {
                style = {
                    paddingTop = 8f, paddingRight = 16f, paddingBottom = 8f, paddingLeft = 16f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            closeButton.RegisterCallback<MouseEnterEvent>(_ => closeButton.style.backgroundColor = s_HoverColor);
            closeButton.RegisterCallback<MouseLeaveEvent>(_ => closeButton.style.backgroundColor = s_BackgroundColor);

            closeButton.RegisterCallback<MouseDownEvent>(_ => { Close(); });

            _panel.Add(title);
            _panel.Add(version);
            _panel.Add(linkContainer);
            _panel.Add(closeButton);
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
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
