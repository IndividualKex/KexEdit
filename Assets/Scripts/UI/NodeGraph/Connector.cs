using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class Connector : VisualElement {
        private VisualElement _circle;
        private VisualElement _cap;

        public VisualElement Circle => _circle;
        public VisualElement Cap => _cap;

        public Connector() {
            style.position = Position.Relative;
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.width = 20f;
            style.height = 20f;

            _circle = new VisualElement {
                style = {
                    position = Position.Relative,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    marginLeft = 4f,
                    marginRight = 4f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    width = 8f,
                    height = 8f,
                    borderTopLeftRadius = 8f,
                    borderTopRightRadius = 8f,
                    borderBottomLeftRadius = 8f,
                    borderBottomRightRadius = 8f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftColor = s_YellowOutline,
                    borderRightColor = s_YellowOutline,
                    borderTopColor = s_YellowOutline,
                    borderBottomColor = s_YellowOutline,
                    backgroundColor = s_DarkBackgroundColor
                }
            };
            Add(_circle);

            _cap = new VisualElement {
                style = {
                    width = 4f,
                    height = 4f,
                    backgroundColor = s_YellowOutline,
                    borderTopLeftRadius = 4f,
                    borderTopRightRadius = 4f,
                    borderBottomLeftRadius = 4f,
                    borderBottomRightRadius = 4f,
                    display = DisplayStyle.None,
                }
            };
            _circle.Add(_cap);
        }
    }
}
