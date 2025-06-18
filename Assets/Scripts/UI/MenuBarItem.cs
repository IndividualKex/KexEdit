using System;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public class MenuBarItem : Label {
        private readonly Action<ContextMenu> _configureMenu;
        private readonly MenuBar _menuBar;
        private bool _isHovered;
        private ContextMenu _activeMenu;

        public MenuBarItem(string text, Action<ContextMenu> configureMenu, MenuBar menuBar) : base(text) {
            _configureMenu = configureMenu;
            _menuBar = menuBar;

            style.paddingLeft = style.paddingRight = 8f;
            style.paddingTop = style.paddingBottom = 2f;
            style.color = new Color(0.7f, 0.7f, 0.7f);
            style.fontSize = 12f;
            style.unityTextAlign = TextAnchor.MiddleCenter;

            RegisterCallback<MouseEnterEvent>(evt => {
                _isHovered = true;
                UpdateVisualState();
                if (_menuBar.HasActiveItem && !_menuBar.IsActiveItem(this)) ShowMenu();
            });

            RegisterCallback<MouseLeaveEvent>(evt => {
                _isHovered = false;
                UpdateVisualState();
            });

            RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 0) {
                    if (_activeMenu != null) _menuBar.ClearActiveItem(); else ShowMenu();
                    evt.StopPropagation();
                }
            });
        }

        private void UpdateVisualState() {
            style.backgroundColor = _activeMenu != null ? s_ActiveColor :
                                  _isHovered ? s_HoverColor : StyleKeyword.None;
        }

        private void ShowMenu() {
            _menuBar.SetActiveItem(this);

            this.ShowContextMenu(new Vector2(0, resolvedStyle.height), menu => {
                _activeMenu = menu;
                _activeMenu.userData = _menuBar;
                UpdateVisualState();
                _configureMenu(menu);
            });
        }

        public void SetInactive() {
            _activeMenu?.parent?.Remove(_activeMenu);
            _activeMenu = null;
            UpdateVisualState();
        }
    }
}
