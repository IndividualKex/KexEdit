using System;
using UnityEngine;
using UnityEngine.UIElements;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public class MenuBar : VisualElement {
        private MenuBarItem _activeItem;

        public bool HasActiveItem => _activeItem != null;

        public MenuBar() {
            style.flexDirection = FlexDirection.Row;
            style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            style.height = Length.Percent(100f);
            style.width = Length.Percent(100f);
            style.alignItems = Align.Stretch;
        }

        public void AddMenu(string text, Action<ContextMenu> configureMenu) {
            Add(new MenuBarItem(text, configureMenu, this));
        }

        public void SetActiveItem(MenuBarItem item) {
            if (_activeItem == item) return;

            _activeItem?.SetInactive();
            _activeItem = item;
        }

        public void ClearActiveItem() {
            _activeItem?.SetInactive();
            _activeItem = null;
        }

        public bool IsActiveItem(MenuBarItem item) => _activeItem == item;
    }
}
