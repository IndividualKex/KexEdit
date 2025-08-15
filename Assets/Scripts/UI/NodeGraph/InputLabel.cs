using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class InputLabel : VisualElement {
        private PortData _data;

        public PortData Data => _data;

        public InputLabel(PortData data) {
            _data = data;

            style.position = Position.Absolute;
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.FlexStart;
            style.top = 0f;
            style.marginTop = -18f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;

            string name = _data.Port.Type.GetDisplayName(_data.Port.IsInput);
            var label = new Label(name) {
                style = {
                    fontSize = 10f,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = s_MutedTextColor
                }
            };
            Add(label);
        }
    }
}
