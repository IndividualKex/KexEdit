using Unity.Properties;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public class CurveButton : VisualElement {
        private TimelineData _data;

        public CurveButton() {
            style.width = 14f;
            style.height = 14f;
            style.backgroundColor = s_BackgroundColor;
            style.unityBackgroundImageTintColor = s_TextColor;
            style.borderTopWidth = 0f;
            style.borderBottomWidth = 0f;
            style.borderLeftWidth = 0f;
            style.borderRightWidth = 0f;
            style.marginLeft = 8f;
            style.marginRight = 2f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
        }

        public void Initialize(TimelineData data) {
            _data = data;

            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);

            var backgroundBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.ViewMode)),
                bindingMode = BindingMode.ToTarget,
            };
            backgroundBinding.sourceToUiConverters.AddConverter((ref TimelineViewMode viewMode) =>
                (StyleColor)(viewMode == TimelineViewMode.Curve ? s_ActiveColor : s_BackgroundColor));

            var tintBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.ViewMode)),
                bindingMode = BindingMode.ToTarget,
            };
            tintBinding.sourceToUiConverters.AddConverter((ref TimelineViewMode viewMode) =>
                (StyleColor)(viewMode == TimelineViewMode.Curve ? s_ActiveTextColor : s_TextColor));

            style.backgroundImage = UIService.Instance.CurveButtonTexture;

            SetBinding("style.backgroundColor", backgroundBinding);
            SetBinding("style.unityBackgroundImageTintColor", tintBinding);
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            if (_data.ViewMode != TimelineViewMode.Curve) {
                style.backgroundColor = s_HoverColor;
            }
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            if (_data.ViewMode != TimelineViewMode.Curve) {
                style.backgroundColor = s_BackgroundColor;
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button != 0 && evt.button != 1) return;
            var e = this.GetPooled<CurveButtonClickEvent>();
            e.MousePosition = evt.localMousePosition;
            e.IsRightClick = evt.button == 1;
            this.Send(e);
            evt.StopPropagation();
        }
    }
}
