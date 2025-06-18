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

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<CurveButtonClickEvent>(OnCurveButtonClick);

            UpdateStyle();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) {
            if (UIService.Instance == null) return;
            style.backgroundImage = UIService.Instance.CurveButtonTexture;
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

        private void OnCurveButtonClick(CurveButtonClickEvent evt) {
            schedule.Execute(UpdateStyle);
        }

        private void UpdateStyle() {
            bool active = _data.ViewMode == TimelineViewMode.Curve;
            style.backgroundColor = active ? s_ActiveColor : s_BackgroundColor;
            style.unityBackgroundImageTintColor = active ? s_ActiveTextColor : s_TextColor;
        }
    }
}
