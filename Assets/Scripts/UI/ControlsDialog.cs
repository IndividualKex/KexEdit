using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class ControlsDialog : VisualElement {
        private Action _onClose;

        public ControlsDialog(Action onClose) {
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
                    paddingTop = 20f, paddingRight = 20f, paddingBottom = 20f, paddingLeft = 20f,
                    minWidth = 720f
                }
            };

            var title = new Label("Controls") {
                style = {
                    fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 16f, unityTextAlign = TextAnchor.UpperCenter
                }
            };

            var columnsContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    marginBottom = 16f,
                    flexGrow = 1f
                }
            };

            var controlsData = new[] {
                ("File Operations", new[] {
                    ("Ctrl+N".ToPlatformShortcut(), "New Project"),
                    ("Ctrl+O".ToPlatformShortcut(), "Open Project"),
                    ("Ctrl+S".ToPlatformShortcut(), "Save Project"),
                }),
                ("Edit Operations", new[] {
                    ("Ctrl+Z".ToPlatformShortcut(), "Undo"),
                    ("Ctrl+Y".ToPlatformShortcut(), "Redo"),
                    ("Ctrl+X".ToPlatformShortcut(), "Cut"),
                    ("Ctrl+C".ToPlatformShortcut(), "Copy"),
                    ("Ctrl+V".ToPlatformShortcut(), "Paste"),
                    ("Del", "Delete")
                }),
                ("Camera & Navigation", new[] {
                    ("R", "Toggle Ride Camera"),
                    ("F", "Focus on Selection"),
                    ("Alt + Mouse Drag".ToPlatformShortcut(), "Orbit Camera"),
                    ("Middle Mouse Drag", "Pan Camera"),
                    ("Alt + Right Mouse Drag".ToPlatformShortcut(), "Pan Camera"),
                    ("Mouse Wheel", "Zoom")
                }),
                ("Free Look Mode", new[] {
                    ("Right-Click + Hold", "Activate Free Look"),
                    ("  + Mouse Move", "Look Around"),
                    ("  + W/A/S/D", "Move Camera"),
                    ("  + Q/E", "Move Up/Down"),
                    ("  + Shift", "Fast Movement"),
                    ("  + Mouse Wheel", "Adjust Fly Speed")
                }),
                ("Node Graph", new[] {
                    ("Ctrl+A".ToPlatformShortcut(), "Select All"),
                    ("Alt+A".ToPlatformShortcut(), "Deselect All")
                }),
                ("Timeline", new[] {
                    ("Mouse Wheel", "Zoom Timeline"),
                    ("Middle Mouse Drag", "Pan Timeline"),
                    ("Alt + Right Mouse Drag".ToPlatformShortcut(), "Pan Timeline"),
                    ("I", "Add Keyframe at Current Time")
                }),
                ("General", new[] {
                    ("Space", "Pause/Unpause"),
                    ("F1", "Toggle Speed Visualization (Experimental)"),
                    ("Ctrl+H".ToPlatformShortcut(), "Show Controls")
                })
            };

            var leftColumn = new VisualElement { style = { width = Length.Percent(30f), marginLeft = 20f, marginRight = 20f } };
            var centerColumn = new VisualElement { style = { width = Length.Percent(30f), marginRight = 20f } };
            var rightColumn = new VisualElement { style = { width = Length.Percent(30f) } };

            var columns = new[] { leftColumn, centerColumn, rightColumn };
            for (int i = 0; i < controlsData.Length; i++) {
                var (sectionTitle, controls) = controlsData[i];
                var targetColumn = columns[i % 3];

                var section = new VisualElement {
                    style = { marginBottom = 20f }
                };

                var sectionLabel = new Label(sectionTitle) {
                    style = {
                        fontSize = 13, unityFontStyleAndWeight = FontStyle.Bold,
                        color = new Color(0.9f, 0.9f, 0.9f, 1f),
                        marginBottom = 8f
                    }
                };
                section.Add(sectionLabel);

                foreach (var (key, description) in controls) {
                    var controlRow = new VisualElement {
                        style = {
                            flexDirection = FlexDirection.Column,
                            marginBottom = 6f,
                            paddingLeft = 8f
                        }
                    };

                    var keyLabel = new Label(key) {
                        style = {
                            fontSize = 11,
                            color = new Color(0.8f, 0.8f, 0.8f, 1f),
                            unityFontStyleAndWeight = FontStyle.Bold,
                            marginBottom = 2f
                        }
                    };

                    var descLabel = new Label(description) {
                        style = {
                            fontSize = 10,
                            color = new Color(s_TextColor.r * 0.85f, s_TextColor.g * 0.85f, s_TextColor.b * 0.85f, 1f),
                            whiteSpace = WhiteSpace.Normal,
                            width = Length.Percent(100f)
                        }
                    };

                    controlRow.Add(keyLabel);
                    controlRow.Add(descLabel);
                    section.Add(controlRow);
                }

                targetColumn.Add(section);
            }

            columnsContainer.Add(leftColumn);
            columnsContainer.Add(centerColumn);
            columnsContainer.Add(rightColumn);

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

            closeButton.RegisterCallback<MouseDownEvent>(_ => {
                Close();
            });

            panel.Add(title);
            panel.Add(columnsContainer);
            panel.Add(closeButton);
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
            panel.Focus();
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
