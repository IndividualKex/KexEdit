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

            float availableWidth = Mathf.Min(Screen.width * 0.8f, 1200f);
            float minPanelWidth = Mathf.Max(availableWidth * 0.8f, 600f);

            var panel = new VisualElement {
                style = {
                    backgroundColor = s_BackgroundColor,
                    borderTopLeftRadius = 3f, borderTopRightRadius = 3f,
                    borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f,
                    borderTopWidth = 1f, borderRightWidth = 1f, borderBottomWidth = 1f, borderLeftWidth = 1f,
                    borderTopColor = s_BorderColor, borderRightColor = s_BorderColor,
                    borderBottomColor = s_BorderColor, borderLeftColor = s_BorderColor,
                    paddingTop = 20f, paddingRight = 20f, paddingBottom = 20f, paddingLeft = 20f,
                    minWidth = minPanelWidth,
                    maxWidth = availableWidth
                }
            };

            var title = new Label("Controls") {
                style = {
                    fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_TextColor, marginBottom = 16f, unityTextAlign = TextAnchor.UpperCenter
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
                    ("Del", "Delete"),
                    ("T", "Sync Playback")
                }),
                ("Camera & Navigation", new[] {
                    ("R", "Toggle Ride Camera"),
                    ("F", "Focus on Selection"),
                    ("Alt + Mouse Drag".ToPlatformShortcut(), "Orbit Camera"),
                    ("Middle Mouse Drag", "Pan Camera"),
                    ("Alt + Right Mouse Drag".ToPlatformShortcut(), "Pan Camera"),
                    ("Mouse Wheel", "Zoom")
                }),
                ("Camera Views", new[] {
                    (Preferences.EnableTopRowViewHotkeys ? "1 or Numpad 1" : "Numpad 1", "Front View"),
                    (Preferences.EnableTopRowViewHotkeys ? "Ctrl+1 or Ctrl+Numpad 1".ToPlatformShortcut() : "Ctrl+Numpad 1".ToPlatformShortcut(), "Back View"),
                    (Preferences.EnableTopRowViewHotkeys ? "3 or Numpad 3" : "Numpad 3", "Right View"),
                    (Preferences.EnableTopRowViewHotkeys ? "Ctrl+3 or Ctrl+Numpad 3".ToPlatformShortcut() : "Ctrl+Numpad 3".ToPlatformShortcut(), "Left View"),
                    (Preferences.EnableTopRowViewHotkeys ? "5 or Numpad 5" : "Numpad 5", "Toggle Orthographic"),
                    (Preferences.EnableTopRowViewHotkeys ? "7 or Numpad 7" : "Numpad 7", "Top View"),
                    (Preferences.EnableTopRowViewHotkeys ? "Ctrl+7 or Ctrl+Numpad 7".ToPlatformShortcut() : "Ctrl+Numpad 7".ToPlatformShortcut(), "Bottom View")
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
                    ("I", "Add Keyframe at Current Time"),
                    ("Shift+1-9", "Property Selection")
                }),
                ("Visualization", new[] {
                    ("Ctrl+1".ToPlatformShortcut(), "Velocity Visualization"),
                    ("Ctrl+2".ToPlatformShortcut(), "Curvature Visualization"),
                    ("Ctrl+3".ToPlatformShortcut(), "Normal Force Visualization"),
                    ("Ctrl+4".ToPlatformShortcut(), "Lateral Force Visualization"),
                    ("Ctrl+5".ToPlatformShortcut(), "Roll Speed Visualization"),
                    ("Ctrl+6".ToPlatformShortcut(), "Pitch Speed Visualization"),
                    ("Ctrl+7".ToPlatformShortcut(), "Yaw Speed Visualization"),
                }),
                ("General", new[] {
                    ("Space", "Pause/Unpause"),
                    ("F1", "Toggle Gizmos"),
                    ("F2", "Toggle Grid"),
                    ("F3", "Toggle Stats"),
                    ("F4", "Toggle Node Grid Snapping"),
                    ("Ctrl+H".ToPlatformShortcut(), "Show Controls")
                })
            };

            var columnsContainer = CreateFlexibleLayout(controlsData, availableWidth);

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

        private VisualElement CreateFlexibleLayout((string title, (string key, string description)[] controls)[] controlsData, float availableWidth) {
            var columnsContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart,
                    marginBottom = 16f,
                    flexGrow = 1f,
                    alignItems = Align.FlexStart
                }
            };

            int columnCount = CalculateOptimalColumns(controlsData, availableWidth);
            var columns = CreateColumns(columnCount);

            foreach (var column in columns) {
                columnsContainer.Add(column);
            }

            var sections = CreateSections(controlsData);
            DistributeSectionsBalanced(sections, columns, controlsData);

            return columnsContainer;
        }

        private int CalculateOptimalColumns((string title, (string key, string description)[] controls)[] controlsData, float containerWidth) {
            float targetSectionWidth = 200f;
            int maxColumnsByWidth = Mathf.FloorToInt((containerWidth - 40f) / targetSectionWidth);

            int minColumns = 2;
            int maxColumns = 4;

            float totalEstimatedHeight = 0f;
            foreach (var sectionData in controlsData) {
                totalEstimatedHeight += EstimateSectionHeightFromData(sectionData);
            }

            float targetColumnHeight = 400f;
            int maxColumnsByHeight = Mathf.CeilToInt(totalEstimatedHeight / targetColumnHeight);

            int maxColumnsByContent = Mathf.CeilToInt(controlsData.Length / 2f);

            return Mathf.Clamp(Mathf.Min(maxColumnsByWidth, Mathf.Min(maxColumnsByHeight, maxColumnsByContent)), minColumns, maxColumns);
        }

        private VisualElement[] CreateColumns(int columnCount) {
            var columns = new VisualElement[columnCount];

            for (int i = 0; i < columnCount; i++) {
                columns[i] = new VisualElement {
                    style = {
                        flexGrow = 1f,
                        flexShrink = 1f,
                        minWidth = 180f,
                        maxWidth = 300f,
                        marginLeft = i == 0 ? 20f : 10f,
                        marginRight = i == columnCount - 1 ? 20f : 10f
                    }
                };
            }

            return columns;
        }

        private VisualElement[] CreateSections((string title, (string key, string description)[] controls)[] controlsData) {
            var sections = new VisualElement[controlsData.Length];

            for (int i = 0; i < controlsData.Length; i++) {
                var (sectionTitle, controls) = controlsData[i];

                var section = new VisualElement {
                    style = { marginBottom = 20f }
                };

                var sectionLabel = new Label(sectionTitle) {
                    style = {
                        fontSize = 13,
                        unityFontStyleAndWeight = FontStyle.Bold,
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

                sections[i] = section;
            }

            return sections;
        }

        private void DistributeSectionsBalanced(VisualElement[] sections, VisualElement[] columns, (string title, (string key, string description)[] controls)[] controlsData) {
            var columnHeights = new float[columns.Length];

            for (int i = 0; i < sections.Length; i++) {
                var section = sections[i];
                var sectionData = controlsData[i];
                float estimatedHeight = EstimateSectionHeightFromData(sectionData);

                int targetColumnIndex = 0;
                for (int j = 1; j < columnHeights.Length; j++) {
                    if (columnHeights[j] < columnHeights[targetColumnIndex]) {
                        targetColumnIndex = j;
                    }
                }

                columns[targetColumnIndex].Add(section);
                columnHeights[targetColumnIndex] += estimatedHeight;
            }
        }

        private float EstimateSectionHeightFromData((string title, (string key, string description)[] controls) sectionData) {
            float baseHeight = 40f;
            float itemHeight = 24f;

            return baseHeight + (sectionData.controls.Length * itemHeight);
        }

        private float EstimateSectionHeight(VisualElement section) {
            float baseHeight = 40f;
            float itemHeight = 24f;

            int controlCount = section.childCount - 1;

            return baseHeight + (controlCount * itemHeight);
        }

        private void Close() {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }
    }
}
