using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class InputThumb : VisualElement {
        private InputThumbEdge _edge;

        private PortData _data;

        public PortData Data => _data;

        public InputThumb(PortData data, bool vertical = false) {
            _data = data;

            if (vertical) {
                style.position = Position.Absolute;
                style.flexDirection = FlexDirection.Column;
                style.alignItems = Align.Center;
                style.justifyContent = Justify.FlexStart;
                style.top = 0f;
                style.marginTop = -22f;
                style.paddingLeft = 4f;
                style.paddingRight = 4f;
                style.paddingTop = 4f;
                style.paddingBottom = 4f;
                style.marginLeft = 0f;
                style.marginRight = 0f;
                style.marginBottom = 0f;
                style.backgroundColor = s_AltBackgroundColor;
            }
            else {
                style.position = Position.Absolute;
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Stretch;
                style.height = 18f;
                style.right = 0f;
                style.paddingLeft = 0f;
                style.paddingRight = 0f;
                style.paddingTop = 0f;
                style.paddingBottom = 0f;
                style.marginLeft = 0f;
                style.marginRight = 32f;
                style.marginTop = 0f;
                style.marginBottom = 0f;
                style.backgroundColor = s_AltBackgroundColor;
            }

            var container = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexDirection = vertical ? FlexDirection.Column : FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = vertical ? 0f : 8f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    backgroundColor = Color.clear
                }
            };
            Add(container);

            if (_data.Port.Type == PortType.Anchor) {
                var label = new Label("Anchor") {
                    style = {
                        fontSize = 10f,
                        unityTextAlign = vertical ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft
                    }
                };
                container.Add(label);
            }
            else if (_data.Port.Type == PortType.Path) {
                var label = new Label("Path") {
                    style = {
                        fontSize = 10f,
                        unityTextAlign = vertical ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft
                    }
                };
                container.Add(label);
            }
            else if (_data.Port.Type == PortType.Position ||
                _data.Port.Type == PortType.Rotation) {
                container.Add(new ThumbFloatField(_data, 0));
                container.Add(new ThumbFloatField(_data, 1));
                container.Add(new ThumbFloatField(_data, 2));
            }
            else if (_data.Port.Type == PortType.Duration
                || _data.Port.Type == PortType.Roll
                || _data.Port.Type == PortType.Pitch
                || _data.Port.Type == PortType.Yaw
                || _data.Port.Type == PortType.Velocity
                || _data.Port.Type == PortType.Heart
                || _data.Port.Type == PortType.Friction
                || _data.Port.Type == PortType.Resistance
                || _data.Port.Type == PortType.Radius
                || _data.Port.Type == PortType.Arc
                || _data.Port.Type == PortType.Axis
                || _data.Port.Type == PortType.LeadIn
                || _data.Port.Type == PortType.LeadOut
                || _data.Port.Type == PortType.InWeight
                || _data.Port.Type == PortType.OutWeight
                || _data.Port.Type == PortType.Scale
                || _data.Port.Type == PortType.Start
                || _data.Port.Type == PortType.End) {
                container.Add(new ThumbFloatField(_data, 0));
            }
            else {
                throw new System.NotImplementedException();
            }

            if (vertical) {
                _edge = new InputThumbEdge(this, vertical);
                Add(_edge);
            }
            else {
                var connector = new VisualElement {
                    style = {
                        position = Position.Relative,
                        justifyContent = Justify.Center,
                        alignItems = Align.Center,
                        width = 16f,
                    }
                };
                Add(connector);

                var circle = new VisualElement {
                    style = {
                        position = Position.Relative,
                        justifyContent = Justify.Center,
                        alignItems = Align.Center,
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
                        backgroundColor = s_DarkBackgroundColor
                    }
                };
                connector.Add(circle);

                var cap = new VisualElement {
                    style = {
                        position = Position.Relative,
                        justifyContent = Justify.Center,
                        alignItems = Align.Center,
                        width = 4f,
                        height = 4f,
                        backgroundColor = s_YellowOutline,
                        borderTopLeftRadius = 4f,
                        borderTopRightRadius = 4f,
                        borderBottomLeftRadius = 4f,
                        borderBottomRightRadius = 4f
                    }
                };
                circle.Add(cap);

                _edge = new InputThumbEdge(this, vertical);
                cap.Add(_edge);
            }

            if (_data.Port.Type == PortType.Anchor) {
                Add(new AnchorThumbControl(_data));
            }

            RegisterCallback<MouseOverEvent>(OnMouseOver);
            RegisterCallback<MouseOutEvent>(OnMouseOut);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        private void OnMouseOver(MouseOverEvent evt) {
            evt.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt) {
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            evt.StopPropagation();
        }
    }
}
