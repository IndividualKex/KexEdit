using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
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
                    paddingTop = 20f, paddingRight = 20f, paddingBottom = 20f, paddingLeft = 20f,
                    minWidth = 320f
                }
            };

            var title = new Label($"{Application.productName}") {
                style = {
                    fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 16f, unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var infoContainer = new VisualElement {
                style = {
                    alignItems = Align.Center,
                    marginBottom = 20f
                }
            };

            var version = new Label($"Version {Application.version}") {
                style = {
                    fontSize = 12,
                    color = new Color(s_TextColor.r * 0.85f, s_TextColor.g * 0.85f, s_TextColor.b * 0.85f, 1f),
                    marginBottom = 4f,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            var linkLabel = new Label("Visit Project Page") {
                style = {
                    fontSize = 11,
                    color = new Color(0.4f, 0.8f, 1f, 1f),
                    unityTextAlign = TextAnchor.MiddleCenter
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

            infoContainer.Add(version);
            infoContainer.Add(linkLabel);

            var creditsContainer = CreateCreditsSection();

            var closeButton = new Label("Close") {
                style = {
                    paddingTop = 8f, paddingRight = 16f, paddingBottom = 8f, paddingLeft = 16f,
                    backgroundColor = s_BackgroundColor, color = s_TextColor,
                    alignSelf = Align.Center,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            closeButton.RegisterCallback<MouseEnterEvent>(_ => closeButton.style.backgroundColor = s_HoverColor);
            closeButton.RegisterCallback<MouseLeaveEvent>(_ => closeButton.style.backgroundColor = s_BackgroundColor);

            closeButton.RegisterCallback<MouseDownEvent>(_ => { Close(); });

            _panel.Add(title);
            _panel.Add(infoContainer);
            _panel.Add(creditsContainer);
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

        private VisualElement CreateCreditsSection() {
            var creditsContainer = new VisualElement {
                style = {
                    width = Length.Percent(100),
                    marginBottom = 12f,
                    alignItems = Align.Center
                }
            };

            var creditsData = LoadCreditsData();

            foreach (var category in creditsData.Credits) {
                var categoryContainer = new VisualElement {
                    style = {
                        marginBottom = 12f,
                        alignItems = Align.Center
                    }
                };

                var categoryLabel = new Label(category.Category) {
                    style = {
                        fontSize = 12,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = s_TextColor,
                        marginBottom = 4f,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };

                var namesContainer = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Column,
                        alignItems = Align.Center
                    }
                };

                foreach (var name in category.Names) {
                    var nameLabel = new Label(name) {
                        style = {
                            fontSize = 11,
                            color = new Color(s_TextColor.r * 0.9f, s_TextColor.g * 0.9f, s_TextColor.b * 0.9f, 1f),
                            marginBottom = 2f,
                            unityTextAlign = TextAnchor.MiddleCenter
                        }
                    };

                    namesContainer.Add(nameLabel);
                }

                categoryContainer.Add(categoryLabel);
                categoryContainer.Add(namesContainer);
                creditsContainer.Add(categoryContainer);
            }

            return creditsContainer;
        }

        private CreditsData LoadCreditsData() {
            string creditsPath = Path.Combine(Application.streamingAssetsPath, "credits.json");
            string creditsText = File.ReadAllText(creditsPath);
            return JsonUtility.FromJson<CreditsData>(creditsText);
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
