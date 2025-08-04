using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class NodeGraphPort : VisualElement {
        private static NodeGraphPort s_DraggedPort = null;
        private static NodeGraphPort s_HoveredPort = null;

        public static bool IsDragging => s_DraggedPort != null;

        private Connector _connector;
        private InputThumb _thumb;
        private InputLabel _inputLabel;
        private OutputThumb _outputThumb;
        private Label _label;
        private Label _unitsLabel;
        private NodeGraphEdge _dragEdge;

        private NodeGraphView _view;
        private PortData _data;

        public PortData Data => _data;

        public NodeGraphPort(NodeGraphView view, PortData data, bool connectableOnly = false, bool vertical = false) {
            _view = view;
            _data = data;
            dataSource = _data;

            if (connectableOnly) {
                style.position = Position.Relative;
                style.flexGrow = 0f;
                style.justifyContent = Justify.Center;
                style.alignItems = Align.Center;
                style.width = 20f;
                style.height = 20f;
                style.paddingLeft = 0f;
                style.paddingRight = 0f;
                style.paddingTop = 0f;
                style.paddingBottom = 0f;
                style.marginLeft = 0f;
                style.marginRight = 0f;
                style.marginTop = 0f;
                style.marginBottom = 0f;

                _connector = new Connector();
                Add(_connector);
            } else if (vertical) {
                style.position = Position.Relative;
                style.flexGrow = 0f;
                style.flexDirection = FlexDirection.Column;
                style.justifyContent = Justify.FlexStart;
                style.alignItems = Align.Center;
                style.paddingLeft = 0f;
                style.paddingRight = 0f;
                style.paddingTop = 0f;
                style.paddingBottom = 0f;
                style.marginLeft = 0f;
                style.marginRight = 0f;
                style.marginTop = 0f;
                style.marginBottom = 0f;

                _connector = new Connector();
                Add(_connector);

                if (_data.Port.IsInput && _data.Port.Type == PortType.Anchor) {
                    _thumb = new InputThumb(_data, vertical: true);
                    _connector.Add(_thumb);
                }

                if (_data.Port.IsInput && _data.Port.Type == PortType.Path) {
                    _inputLabel = new InputLabel(_data);
                    Add(_inputLabel);
                }

                if (!_data.Port.IsInput) {
                    _outputThumb = new OutputThumb(_data);
                    Add(_outputThumb);
                }
            } else {
                style.position = Position.Relative;
                style.flexGrow = 0f;
                style.flexDirection = data.Port.IsInput ? FlexDirection.Row : FlexDirection.RowReverse;
                style.justifyContent = Justify.FlexStart;
                style.alignItems = Align.Center;
                style.height = 24f;
                style.paddingLeft = 4f;
                style.paddingRight = 4f;
                style.paddingBottom = 0f;
                style.paddingTop = 0f;
                style.marginLeft = 0f;
                style.marginRight = 0f;
                style.marginTop = 0f;
                style.marginBottom = 0f;

                _connector = new Connector();
                Add(_connector);

                string name = _data.Port.Type.GetDisplayName(_data.Port.IsInput);
                _label = new Label(name) {
                style = {
                    marginLeft = 4f,
                    marginRight = 4f,
                    marginTop = 2f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                }
                };
                Add(_label);

                if (_data.Units != UnitsType.None) {
                    _unitsLabel = new Label {
                        style = {
                            marginLeft = 0f,
                            marginRight = 4f,
                            marginTop = 2f,
                            marginBottom = 0f,
                            paddingLeft = 0f,
                            paddingRight = 0f,
                            paddingTop = 0f,
                            paddingBottom = 0f,
                            color = s_MutedTextColor
                        }
                    };
                    Add(_unitsLabel);
                }

                if (_data.Port.IsInput) {
                    _thumb = new InputThumb(_data);
                    _connector.Add(_thumb);
                }
            }

            _connector.RegisterCallback<MouseOverEvent>(OnMouseOver);
            _connector.RegisterCallback<MouseOutEvent>(OnMouseOut);
            _connector.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _connector.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _connector.RegisterCallback<MouseUpEvent>(OnMouseUp);

            var circleBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PortData.InteractionState)),
                bindingMode = BindingMode.ToTarget
            };
            circleBinding.sourceToUiConverters.AddConverter((ref PortState value) => {
                return (StyleColor)(value.HasFlag(PortState.Dragging) ? Color.clear : s_YellowOutline);
            });
            _connector.Circle.SetBinding("style.borderLeftColor", circleBinding);
            _connector.Circle.SetBinding("style.borderRightColor", circleBinding);
            _connector.Circle.SetBinding("style.borderTopColor", circleBinding);
            _connector.Circle.SetBinding("style.borderBottomColor", circleBinding);

            var capBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(PortData.InteractionState)),
                bindingMode = BindingMode.ToTarget
            };
            capBinding.sourceToUiConverters.AddConverter((ref PortState value) => {
                if (value.HasFlag(PortState.Connected) && !value.HasFlag(PortState.Dragging)) {
                    return new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                }
                else if (value.HasFlag(PortState.Hovered) || value.HasFlag(PortState.Dragging)) {
                    return new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                }
                return new StyleEnum<DisplayStyle>(DisplayStyle.None);
            });
            _connector.Cap.SetBinding("style.display", capBinding);

            if (_thumb != null) {
                var thumbBinding = new DataBinding {
                    dataSourcePath = new PropertyPath(nameof(PortData.InteractionState)),
                    bindingMode = BindingMode.ToTarget
                };
                thumbBinding.sourceToUiConverters.AddConverter((ref PortState value) =>
                    new StyleEnum<DisplayStyle>(value.HasFlag(PortState.Connected) ? DisplayStyle.None : DisplayStyle.Flex)
                );
                _thumb.SetBinding("style.display", thumbBinding);
            }

            if (_inputLabel != null) {
                var inputLabelBinding = new DataBinding {
                    dataSourcePath = new PropertyPath(nameof(PortData.InteractionState)),
                    bindingMode = BindingMode.ToTarget
                };
                inputLabelBinding.sourceToUiConverters.AddConverter((ref PortState value) =>
                    new StyleEnum<DisplayStyle>(value.HasFlag(PortState.Connected) ? DisplayStyle.None : DisplayStyle.Flex)
                );
                _inputLabel.SetBinding("style.display", inputLabelBinding);
            }

            if (_outputThumb != null) {
                var outputThumbBinding = new DataBinding {
                    dataSourcePath = new PropertyPath(nameof(PortData.InteractionState)),
                    bindingMode = BindingMode.ToTarget
                };
                outputThumbBinding.sourceToUiConverters.AddConverter((ref PortState value) =>
                    new StyleEnum<DisplayStyle>(value.HasFlag(PortState.Connected) ? DisplayStyle.None : DisplayStyle.Flex)
                );
                _outputThumb.SetBinding("style.display", outputThumbBinding);
            }

            if (_unitsLabel != null) {
                var unitsBinding = new DataBinding {
                    dataSourcePath = new PropertyPath(nameof(PortData.Units)),
                    bindingMode = BindingMode.ToTarget
                };
                unitsBinding.sourceToUiConverters.AddConverter((ref UnitsType value) => value.ToDisplaySuffix());
                _unitsLabel.SetBinding("text", unitsBinding);
            }
        }

        private void OnMouseOver(MouseOverEvent evt) {
            if (s_DraggedPort != null &&
                (s_DraggedPort == this ||
                s_DraggedPort?.Data.Port.IsInput == _data.Port.IsInput ||
                s_DraggedPort?.Data.Port.Type != _data.Port.Type)) return;

            s_HoveredPort = this;
            _data.InteractionState |= PortState.Hovered;
            evt.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt) {
            if (s_HoveredPort == this) {
                s_HoveredPort = null;
                _data.InteractionState &= ~PortState.Hovered;
            }
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button != 0) return;

            s_DraggedPort = this;
            s_HoveredPort = null;
            EdgeData dummy = new();
            _dragEdge = new NodeGraphEdge(_view, dummy, this);
            _view.EdgeLayer.Add(_dragEdge);
            _dragEdge.SetDragEnd(evt.mousePosition);
            _data.InteractionState |= PortState.Dragging;
            _data.InteractionState &= ~PortState.Hovered;
            _connector.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (s_DraggedPort != this) return;

            _dragEdge.SetDragEnd(evt.mousePosition);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (s_DraggedPort != this) return;

            if (s_HoveredPort != null) {
                NodeGraphPort source = s_HoveredPort.Data.Port.IsInput ? s_DraggedPort : s_HoveredPort;
                NodeGraphPort target = s_HoveredPort.Data.Port.IsInput ? s_HoveredPort : s_DraggedPort;
                Undo.Record();
                var e = this.GetPooled<AddConnectionEvent>();
                e.Source = source.Data;
                e.Target = target.Data;
                _view.SendEvent(e);
            }
            else if (!_data.Port.IsInput) {
                var e = this.GetPooled<DragOutputPortEvent>();
                e.Port = _data;
                e.MousePosition = evt.mousePosition;
                _view.SendEvent(e);
            }

            s_DraggedPort = null;
            _view.EdgeLayer.Remove(_dragEdge);
            _dragEdge = null;
            _data.InteractionState &= ~PortState.Dragging;
            _connector.ReleaseMouse();
            evt.StopPropagation();
        }
    }
}
