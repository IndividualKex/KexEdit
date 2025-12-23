using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class ContextMenu : VisualElement {
        private static readonly float s_MenuOverlap = 1f;
        private static readonly float s_VerticalOffset = 2f;

        private ContextMenu _activeSubmenu;
        private IVisualElementScheduledItem _hideTask;

        public ContextMenu() {
            style.position = Position.Absolute;
            style.backgroundColor = s_BackgroundColor;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;

            style.minWidth = 128f;

            style.borderTopLeftRadius = 3f;
            style.borderTopRightRadius = 3f;
            style.borderBottomLeftRadius = 3f;
            style.borderBottomRightRadius = 3f;

            style.borderTopWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;

            style.borderTopColor = s_BorderColor;
            style.borderRightColor = s_BorderColor;
            style.borderBottomColor = s_BorderColor;
            style.borderLeftColor = s_BorderColor;

            style.opacity = 0f;

            style.transitionProperty = new List<StylePropertyName> { "opacity" };
            style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            schedule.Execute(() => {
                style.opacity = 1f;
            });
        }

        private void HideActiveSubmenu() {
            if (_activeSubmenu?.parent != null) {
                _activeSubmenu.parent.Remove(_activeSubmenu);
            }
            _activeSubmenu = null;
        }

        private void CancelHideTask() {
            if (_hideTask != null) {
                _hideTask.Pause();
                _hideTask = null;
            }
        }

        public void AddItem(string text, Action action, string shortcut = null, bool isChecked = false, bool enabled = true) {
            var container = CreateMenuItemContainer();

            var checkmarkLabel = new Label(isChecked ? "✓" : "") {
                style = {
                    minWidth = 16f,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = enabled ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.4f, 0.4f, 0.4f),
                }
            };

            var textLabel = new Label(text) {
                style = {
                    flexGrow = 1f,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = enabled ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.4f, 0.4f, 0.4f),
                }
            };

            container.Add(checkmarkLabel);
            container.Add(textLabel);

            if (!string.IsNullOrEmpty(shortcut)) {
                var shortcutLabel = new Label(shortcut) {
                    style = {
                        unityTextAlign = TextAnchor.MiddleRight,
                        color = enabled ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f),
                        fontSize = 11f,
                        marginLeft = 12f,
                    }
                };
                container.Add(shortcutLabel);
            }

            if (enabled) {
                container.RegisterCallback<ClickEvent>((evt) => {
                    action?.Invoke();
                    CloseMenuHierarchy();
                });
                SetupHoverEffects(container);
            }

            Add(container);
        }

        public void AddSeparator() {
            var separator = new VisualElement {
                style = {
                    height = 1f,
                    backgroundColor = s_DarkBackgroundColor,
                    marginTop = 3f,
                    marginBottom = 3f,
                    marginLeft = 5f,
                    marginRight = 5f,
                }
            };

            Add(separator);
        }

        public void AddSubmenu(string text, Action<ContextMenu> configureSubmenu) {
            var container = CreateMenuItemContainer();

            var spacer = new VisualElement {
                style = {
                    minWidth = 16f,
                }
            };

            var textLabel = new Label(text) {
                style = {
                    flexGrow = 1f,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = new Color(0.8f, 0.8f, 0.8f),
                }
            };

            var arrowLabel = new Label("►") {
                style = {
                    minWidth = 15f,
                    unityTextAlign = TextAnchor.MiddleRight,
                    color = new Color(0.8f, 0.8f, 0.8f),
                }
            };

            container.Add(spacer);
            container.Add(textLabel);
            container.Add(arrowLabel);

            void ShowSubmenu() {
                CancelHideTask();
                HideActiveSubmenu();

                _activeSubmenu = new ContextMenu();
                _activeSubmenu.style.position = Position.Absolute;

                configureSubmenu(_activeSubmenu);

                var tempContainer = new VisualElement();
                tempContainer.style.position = Position.Absolute;
                tempContainer.style.left = -10000f;
                tempContainer.style.top = -10000f;
                tempContainer.Add(_activeSubmenu);
                Add(tempContainer);

                bool repositioned = false;
                void OnGeometryChanged(GeometryChangedEvent evt) {
                    if (!repositioned && _activeSubmenu.resolvedStyle.width > 0 && _activeSubmenu.resolvedStyle.height > 0) {
                        repositioned = true;
                        _activeSubmenu.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

                        Vector2 adjustedPos = CalculateSubmenuPosition(_activeSubmenu, container);

                        if (tempContainer.parent == this) {
                            tempContainer.Remove(_activeSubmenu);
                            Remove(tempContainer);
                        }

                        _activeSubmenu.style.left = adjustedPos.x;
                        _activeSubmenu.style.top = adjustedPos.y;
                        Add(_activeSubmenu);
                    }
                }
                _activeSubmenu.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

                _activeSubmenu.RegisterCallback<MouseEnterEvent>((evt) => CancelHideTask());
                _activeSubmenu.RegisterCallback<MouseLeaveEvent>((evt) => {
                    _hideTask = schedule.Execute(HideActiveSubmenu);
                    _hideTask.ExecuteLater(100);
                });
            }

            container.RegisterCallback<MouseEnterEvent>((evt) => {
                ShowSubmenu();
            });

            container.RegisterCallback<MouseLeaveEvent>((evt) => {
                _hideTask = schedule.Execute(HideActiveSubmenu);
                _hideTask.ExecuteLater(100);
            });

            SetupSubmenuHoverEffects(container);
            Add(container);
        }

        private VisualElement CreateMenuItemContainer() {
            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = StyleKeyword.None,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    paddingTop = 5f,
                    paddingBottom = 5f,
                    width = Length.Percent(100f),
                }
            };

            return container;
        }

        private void SetupHoverEffects(VisualElement container) {
            container.RegisterCallback<MouseEnterEvent>((evt) => {
                container.style.backgroundColor = s_HoverColor;
                HideActiveSubmenu();
            });
            container.RegisterCallback<MouseLeaveEvent>((evt) => {
                container.style.backgroundColor = StyleKeyword.None;
            });
        }

        private void SetupSubmenuHoverEffects(VisualElement container) {
            container.RegisterCallback<MouseEnterEvent>((evt) => {
                container.style.backgroundColor = s_HoverColor;
            });
            container.RegisterCallback<MouseLeaveEvent>((evt) => {
                container.style.backgroundColor = StyleKeyword.None;
            });
        }

        private Vector2 CalculateSubmenuPosition(ContextMenu submenu, VisualElement submenuContainer) {
            var root = panel.visualTree.Q<TemplateContainer>();
            var rootWorldBound = root.worldBound;
            var submenuRect = submenu.resolvedStyle;
            var parentWorldBound = worldBound;
            var containerWorldBound = submenuContainer.worldBound;

            float submenuWidth = submenuRect.width;
            float submenuHeight = submenuRect.height;

            float defaultX = parentWorldBound.xMax - s_MenuOverlap;
            float defaultY = containerWorldBound.y - s_VerticalOffset;

            float adjustedX = defaultX;
            float adjustedY = defaultY;

            if (defaultX + submenuWidth > rootWorldBound.xMax) {
                adjustedX = parentWorldBound.x - submenuWidth - s_MenuOverlap;
            }

            if (defaultY + submenuHeight > rootWorldBound.yMax) {
                adjustedY = Mathf.Max(rootWorldBound.y, containerWorldBound.yMax - submenuHeight);
            }

            return new Vector2(adjustedX - parentWorldBound.x, adjustedY - parentWorldBound.y);
        }

        public static Vector2 CalculateMenuPosition(VisualElement targetContainer, Vector2 requestedPosition, Vector2 menuSize) {
            var containerRect = targetContainer.resolvedStyle;
            float containerWidth = containerRect.width;
            float containerHeight = containerRect.height;

            float adjustedX = requestedPosition.x;
            float adjustedY = requestedPosition.y;

            if (requestedPosition.x + menuSize.x > containerWidth) {
                adjustedX = Mathf.Max(0, requestedPosition.x - menuSize.x);
            }

            if (requestedPosition.y + menuSize.y > containerHeight) {
                adjustedY = Mathf.Max(0, requestedPosition.y - menuSize.y);
            }

            return new Vector2(adjustedX, adjustedY);
        }

        private void CloseMenuHierarchy() {
            var rootMenu = FindRootMenu();
            (rootMenu?.userData as MenuBar)?.ClearActiveItem();
            rootMenu?.parent?.Remove(rootMenu);
        }

        private ContextMenu FindRootMenu() {
            ContextMenu current = this;
            while (current.parent is ContextMenu parentMenu) {
                current = parentMenu;
            }
            return current;
        }
    }
}
