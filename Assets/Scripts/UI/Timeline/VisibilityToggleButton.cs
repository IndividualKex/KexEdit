using Unity.Properties;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.Timeline {
    public class VisibilityToggleButton : VisualElement {
        private PropertyData _data;
        private Image _iconImage;
        private bool _hover;

        public VisibilityToggleButton(PropertyData data) {
            _data = data;

            style.width = 12f;
            style.height = 12f;
            style.marginLeft = 4f;
            style.marginRight = 4f;
            style.marginTop = 4f;
            style.marginBottom = 0f;
            style.backgroundColor = s_AltDarkBackgroundColor;

            focusable = true;

            _iconImage = new Image() {
                tintColor = s_MutedTextColor,
                style = {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                }
            };
            Add(_iconImage);

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

            var hiddenBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PropertyData.Hidden)),
                bindingMode = BindingMode.ToTarget
            };
            hiddenBinding.sourceToUiConverters.AddConverter((ref bool hidden) => {
                UpdateIcon();
                return "";
            });
            SetBinding("tooltip", hiddenBinding);

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PropertyData.ViewMode)),
                bindingMode = BindingMode.ToTarget
            };
            displayBinding.sourceToUiConverters.AddConverter((ref TimelineViewMode value) =>
                new StyleEnum<DisplayStyle>(value == TimelineViewMode.Curve ? DisplayStyle.Flex : DisplayStyle.None));
            SetBinding("style.display", displayBinding);

            UpdateIcon();
        }

        private void UpdateIcon() {
            var uiService = UIService.Instance;
            _iconImage.image = _data.Hidden ? uiService.EyeXTexture : uiService.EyeTexture;
            _iconImage.tintColor = _data.Hidden ? (_hover ? s_ActiveHoverColor : s_ActiveColor) :
                (_hover ? s_ActiveTextColor : s_MutedTextColor);
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0) {
                _data.Hidden = !_data.Hidden;
                UpdateIcon();
                MarkDirtyRepaint();
                evt.StopPropagation();
            }
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            _hover = true;
            UpdateIcon();
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            _hover = false;
            UpdateIcon();
        }
    }
}
