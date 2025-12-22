using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI.Timeline {
    public class AddPropertyButton : Label {
        public AddPropertyButton() {
            text = "+ Add Property";

            style.height = 20f;
            style.width = 120f;
            style.maxWidth = Length.Percent(100);
            style.alignSelf = Align.Center;
            style.backgroundColor = s_BackgroundColor;
            style.borderTopWidth = 0f;
            style.borderBottomWidth = 0f;
            style.borderLeftWidth = 0f;
            style.borderRightWidth = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 8f;
            style.marginBottom = 0f;
            style.paddingLeft = 8f;
            style.paddingRight = 8f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.unityTextAlign = TextAnchor.MiddleCenter;
            style.fontSize = 11;
            style.color = new Color(0.7f, 0.7f, 0.7f);
            style.display = DisplayStyle.None;

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.AddPropertyButtonVisible)),
                bindingMode = BindingMode.ToTarget
            };
            displayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleEnum<DisplayStyle>(value ? DisplayStyle.Flex : DisplayStyle.None));
            SetBinding("style.display", displayBinding);

        }

        public void Initialize(TimelineData data) {
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            style.backgroundColor = s_HoverColor;
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            style.backgroundColor = s_BackgroundColor;
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button != 0) return;
            var e = this.GetPooled<AddPropertyClickEvent>();
            e.MousePosition = evt.localMousePosition;
            this.Send(e);
            evt.StopPropagation();
        }
    }
}
