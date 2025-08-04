using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public class ReadOnlyButton : Label {
        private TimelineData _data;

        public ReadOnlyButton() {
            style.width = 14f;
            style.height = 14f;
            style.backgroundColor = s_BackgroundColor;
            style.borderTopWidth = 0f;
            style.borderBottomWidth = 0f;
            style.borderLeftWidth = 0f;
            style.borderRightWidth = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.unityTextAlign = TextAnchor.MiddleCenter;
            style.fontSize = 6f;
            style.color = s_TextColor;

            text = "▼";
        }

        public void Initialize(TimelineData data) {
            _data = data;

            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);

            var backgroundBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.DrawAnyReadOnly)),
                bindingMode = BindingMode.ToTarget,
            };
            backgroundBinding.sourceToUiConverters.AddConverter((ref bool drawAnyReadOnly) =>
                (StyleColor)(drawAnyReadOnly ? s_ActiveColor : s_BackgroundColor));

            var colorBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.DrawAnyReadOnly)),
                bindingMode = BindingMode.ToTarget,
            };
            colorBinding.sourceToUiConverters.AddConverter((ref bool drawAnyReadOnly) =>
                (StyleColor)(drawAnyReadOnly ? s_ActiveTextColor : s_TextColor));

            SetBinding("style.backgroundColor", backgroundBinding);
            SetBinding("style.color", colorBinding);
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            if (!_data.DrawAnyReadOnly) {
                style.backgroundColor = s_HoverColor;
            }
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            if (!_data.DrawAnyReadOnly) {
                style.backgroundColor = s_BackgroundColor;
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button != 0) return;
            var e = this.GetPooled<ReadOnlyButtonClickEvent>();
            e.MousePosition = evt.localMousePosition;
            this.Send(e);
            evt.StopPropagation();
        }
    }
}
