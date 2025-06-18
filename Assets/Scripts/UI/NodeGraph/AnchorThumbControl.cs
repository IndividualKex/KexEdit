using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class AnchorThumbControl : VisualElement {
        private PortData _data;

        public AnchorThumbControl(PortData data) {
            _data = data;

            style.position = Position.Absolute;
            style.left = -2f;
            style.right = -2f;
            style.top = -2f;
            style.bottom = -2f;
            style.borderBottomWidth = 2f;
            style.borderTopWidth = 2f;
            style.borderLeftWidth = 2f;
            style.borderRightWidth = 2f;
            style.borderBottomColor = Color.clear;
            style.borderTopColor = Color.clear;
            style.borderLeftColor = Color.clear;
            style.borderRightColor = Color.clear;
            style.backgroundColor = Color.clear;

            RegisterCallback<MouseOverEvent>(OnMouseOver);
            RegisterCallback<MouseOutEvent>(OnMouseOut);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<ClickEvent>(OnClick);
        }

        private void OnMouseOver(MouseOverEvent evt) {
            SetBorderColor(s_BlueOutlineTransparent);
            evt.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt) {
            SetBorderColor(Color.clear);
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 1 && !evt.altKey) {
                this.ShowContextMenu(evt.localMousePosition, menu => {
                    menu.AddItem("Promote", () => {
                        Promote();
                    });
                });
            }
        }

        private void OnClick(ClickEvent evt) {
            if (evt.clickCount == 2) {
                Promote();
                evt.StopPropagation();
            }
        }

        private void Promote() {
            Undo.Record();
            var e = this.GetPooled<AnchorPromoteEvent>();
            e.Port = _data;
            this.SendEvent(e);
        }

        private void SetBorderColor(Color color) {
            style.borderBottomColor = color;
            style.borderTopColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
        }
    }
}
