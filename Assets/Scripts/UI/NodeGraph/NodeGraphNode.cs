using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class NodeGraphNode : VisualElement {
        private VisualElement _topPortsContainer;
        private VisualElement _mainBody;
        private VisualElement _contentArea;
        private VisualElement _header;
        private VisualElement _headerDivider;
        private VisualElement _contents;
        private VisualElement _inputsContainer;
        private VisualElement _footerDivider;
        private VisualElement _footer;
        private DurationTypeField _durationTypeField;
        private RenderToggle _renderToggle;
        private PriorityField _priorityField;
        private VisualElement _foldout;
        private VisualElement _itemsContainer;
        private Label _collapseButton;
        private VisualElement _bottomPortsContainer;

        private Dictionary<Entity, NodeGraphPort> _ports = new();
        private NodeGraphView _view;
        private NodeType _type;
        private NodeData _data;
        private DataBinding _interactionStateBinding;
        private DataBinding _leftBinding;
        private DataBinding _topBinding;
        private Vector2 _startMousePosition;
        private bool _dragging;
        private bool _moved;

        public NodeType Type => _type;
        public NodeData Data => _data;
        public Dictionary<Entity, NodeGraphPort> Ports => _ports;

        public NodeGraphNode(NodeGraphView view, NodeType type) {
            _view = view;
            _type = type;

            style.position = Position.Absolute;
            style.backgroundColor = Color.clear;
            style.flexGrow = 1f;
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Stretch;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.borderTopWidth = 2f;
            style.borderBottomWidth = 2f;
            style.borderLeftWidth = 2f;
            style.borderRightWidth = 2f;
            style.borderTopColor = Color.clear;
            style.borderBottomColor = Color.clear;
            style.borderLeftColor = Color.clear;
            style.borderRightColor = Color.clear;
            style.transitionProperty = new List<StylePropertyName> {
                "style.borderTopColor",
                "style.borderBottomColor",
                "style.borderLeftColor",
                "style.borderRightColor"
            };
            style.transitionDuration = new List<TimeValue> { new(100, TimeUnit.Millisecond) };
            style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            _topPortsContainer = new VisualElement {
                name = "TopPortsContainer",
                style = {
                    position = Position.Absolute,
                    top = -10f,
                    left = 0f,
                    right = 0f,
                    height = 20f,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = -24f,
                    marginTop = 0f,
                    marginBottom = 0f
                },
                pickingMode = PickingMode.Ignore
            };

            _mainBody = new VisualElement {
                name = "MainBody",
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Stretch,
                    flexGrow = 1f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    backgroundColor = s_AltBackgroundColor
                }
            };

            _contentArea = new VisualElement {
                name = "ContentArea",
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.Stretch,
                    flexGrow = 1f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    minWidth = 144f
                }
            };

            _header = new VisualElement {
                name = "Header",
                style = {
                    position = Position.Relative,
                    flexGrow = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 6f,
                    paddingBottom = 4f,
                }
            };
            _header.Add(new Label(_type.GetDisplayName()) {
                style = {
                    marginLeft = 8f,
                    marginRight = 8f,
                    marginTop = 6f,
                    marginBottom = 6f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f
                }
            });
            _contentArea.Add(_header);

            _headerDivider = new VisualElement {
                name = "HeaderDivider",
                style = {
                    position = Position.Relative,
                    height = 1f,
                    flexGrow = 0f,
                    backgroundColor = s_DividerColor,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f
                }
            };
            _contentArea.Add(_headerDivider);

            _inputsContainer = new VisualElement {
                name = "InputsContainer",
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.FlexStart,
                    alignItems = Align.FlexStart,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f
                }
            };
            _contentArea.Add(_inputsContainer);

            _footerDivider = new VisualElement {
                name = "FooterDivider",
                style = {
                    position = Position.Relative,
                    height = 1f,
                    flexGrow = 0f,
                    backgroundColor = s_DividerColor,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f
                }
            };
            _contentArea.Add(_footerDivider);

            _footer = new VisualElement {
                name = "Footer",
                style = {
                    position = Position.Relative,
                    height = 20f,
                    flexGrow = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f
                }
            };
            _contentArea.Add(_footer);

            _foldout = new VisualElement {
                name = "Foldout",
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Stretch,
                    flexGrow = 0f,
                    flexShrink = 0f,
                    width = 24f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    borderLeftWidth = 1f,
                    borderLeftColor = s_DividerColor
                }
            };
            _contentArea.Add(_topPortsContainer);
            _mainBody.Add(_contentArea);
            _mainBody.Add(_foldout);
            Add(_mainBody);

            _bottomPortsContainer = new VisualElement {
                name = "BottomPortsContainer",
                style = {
                    position = Position.Absolute,
                    bottom = -10f,
                    left = 0f,
                    right = 0f,
                    height = 20f,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = -24f,
                    marginTop = 0f,
                    marginBottom = 0f
                },
                pickingMode = PickingMode.Ignore
            };
            _contentArea.Add(_bottomPortsContainer);

            if (_type == NodeType.ForceSection
                || _type == NodeType.GeometricSection
                || _type == NodeType.CurvedSection
                || _type == NodeType.CopyPathSection
                || _type == NodeType.Reverse
                || _type == NodeType.ReversePath
                || _type == NodeType.Bridge
                || _type == NodeType.Mesh) {
                _itemsContainer = new VisualElement {
                    name = "ItemsContainer",
                    style = {
                        position = Position.Relative,
                        flexDirection = FlexDirection.Column,
                        alignItems = Align.Stretch,
                        justifyContent = Justify.FlexStart,
                        flexGrow = 1f,
                        paddingLeft = 4f,
                        paddingRight = 4f,
                        paddingTop = 4f,
                        paddingBottom = 4f,
                        marginLeft = 0f,
                        marginRight = 0f,
                        marginTop = 0f,
                        marginBottom = 0f,
                        display = DisplayStyle.None
                    }
                };
                _foldout.Add(_itemsContainer);

                if (_type == NodeType.ForceSection
                    || _type == NodeType.GeometricSection) {
                    _durationTypeField = new DurationTypeField();
                    _itemsContainer.Add(_durationTypeField);
                }

                if (_type == NodeType.ForceSection
                    || _type == NodeType.GeometricSection
                    || _type == NodeType.CurvedSection
                    || _type == NodeType.CopyPathSection
                    || _type == NodeType.Bridge
                    || _type == NodeType.Mesh) {
                    _renderToggle = new RenderToggle();
                    _itemsContainer.Add(_renderToggle);
                }

                if (_type != NodeType.Mesh && _type != NodeType.Append) {
                    _priorityField = new PriorityField();
                    _itemsContainer.Add(_priorityField);
                }

                _collapseButton = new Label("►") {
                    name = "CollapseButton",
                    style = {
                        position = Position.Relative,
                        flexGrow = 0f,
                        flexShrink = 0f,
                        width = 24f,
                        paddingLeft = 0f,
                        paddingRight = 0f,
                        paddingTop = 0f,
                        paddingBottom = 0f,
                        marginLeft = 0f,
                        marginRight = 0f,
                        marginTop = 0f,
                        marginBottom = 0f,
                        backgroundColor = Color.clear,
                        fontSize = 9f,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        justifyContent = Justify.Center,
                        alignItems = Align.Center
                    }
                };
                _foldout.Add(_collapseButton);
            }

            _interactionStateBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(NodeData.InteractionState)),
                bindingMode = BindingMode.ToTarget
            };
            _interactionStateBinding.sourceToUiConverters.AddConverter((ref InteractionState value) => {
                if (value.HasFlag(InteractionState.Selected)) {
                    return (StyleColor)s_BlueOutline;
                }
                else if (value.HasFlag(InteractionState.Hovered)) {
                    return (StyleColor)s_BlueOutlineTransparent;
                }
                else {
                    return (StyleColor)Color.clear;
                }
            });

            _leftBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(NodeData.Position)),
                bindingMode = BindingMode.ToTarget,

            };
            _leftBinding.sourceToUiConverters.AddConverter((ref float2 value) => new StyleLength(value.x));

            _topBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(NodeData.Position)),
                bindingMode = BindingMode.ToTarget
            };
            _topBinding.sourceToUiConverters.AddConverter((ref float2 value) => new StyleLength(value.y));

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void Bind(NodeData data) {
            _data = data;
            dataSource = _data;

            _durationTypeField?.Bind(_data);
            _renderToggle?.Bind(_data);
            _priorityField?.Bind(_data);

            bool hasNonVerticalInputs = false;
            foreach (var portData in _data.Inputs.Values) {
                if (portData.Port.Type == PortType.Anchor || portData.Port.Type == PortType.Path) {
                    var port = new NodeGraphPort(_view, portData, vertical: true);
                    _ports.Add(portData.Entity, port);
                    _topPortsContainer.Add(port);
                }
                else {
                    var port = new NodeGraphPort(_view, portData);
                    _ports.Add(portData.Entity, port);
                    _inputsContainer.Add(port);
                    hasNonVerticalInputs = true;
                }
            }

            _inputsContainer.style.display = hasNonVerticalInputs ? DisplayStyle.Flex : DisplayStyle.None;
            _footerDivider.style.display = hasNonVerticalInputs ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var portData in _data.Outputs.Values) {
                var port = new NodeGraphPort(_view, portData, vertical: true);
                _ports.Add(portData.Entity, port);
                _bottomPortsContainer.Add(port);
            }

            AdjustConnectablePortSpacing();

            SetBinding("style.borderTopColor", _interactionStateBinding);
            SetBinding("style.borderBottomColor", _interactionStateBinding);
            SetBinding("style.borderLeftColor", _interactionStateBinding);
            SetBinding("style.borderRightColor", _interactionStateBinding);
            SetBinding("style.left", _leftBinding);
            SetBinding("style.top", _topBinding);
        }

        private void AdjustConnectablePortSpacing() {
            PositionPortsAbsolute(_topPortsContainer);
            PositionPortsAbsolute(_bottomPortsContainer);
        }

        private void PositionPortsAbsolute(VisualElement container) {
            int anchorCount = 0;
            int pathCount = 0;
            
            for (int i = 0; i < container.childCount; i++) {
                if (container[i] is NodeGraphPort port) {
                    if (port.Data.Port.Type == PortType.Anchor) {
                        anchorCount++;
                    }
                    else if (port.Data.Port.Type == PortType.Path) {
                        pathCount++;
                    }
                }
            }
            
            int anchorIndex = 0;
            int pathIndex = 0;
            
            for (int i = 0; i < container.childCount; i++) {
                if (container[i] is NodeGraphPort port) {
                    port.style.position = Position.Absolute;
                    port.style.top = 0;

                    if (port.Data.Port.Type == PortType.Anchor) {
                        if (_type == NodeType.Bridge && anchorCount == 2) {
                            port.style.left = new StyleLength(new Length(anchorIndex == 0 ? 33.33f : 66.67f, LengthUnit.Percent));
                        }
                        else {
                            port.style.left = new StyleLength(new Length(33.33f, LengthUnit.Percent));
                        }
                        anchorIndex++;
                    }
                    else if (port.Data.Port.Type == PortType.Path) {
                        port.style.left = new StyleLength(new Length(66.67f, LengthUnit.Percent));
                        pathIndex++;
                    }

                    port.style.translate = new StyleTranslate(new Translate(new Length(-50f, LengthUnit.Percent), 0));
                }
            }
        }

        private void Unbind() {
            _data = null;
            dataSource = null;

            _durationTypeField?.Unbind();
            _renderToggle?.Unbind();
            _priorityField?.Unbind();

            _inputsContainer.Clear();
            _topPortsContainer.Clear();
            _bottomPortsContainer.Clear();
            _ports.Clear();

            ClearBindings();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt) {
            RegisterCallback<MouseOverEvent>(OnMouseOver);
            RegisterCallback<MouseOutEvent>(OnMouseOut);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);

            _collapseButton?.RegisterCallback<MouseDownEvent>(OnCollapseButtonMouseDown);
            _collapseButton?.RegisterCallback<MouseOverEvent>(OnCollapseButtonMouseOver);
            _collapseButton?.RegisterCallback<MouseOutEvent>(OnCollapseButtonMouseOut);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt) {
            UnregisterCallback<MouseOverEvent>(OnMouseOver);
            UnregisterCallback<MouseOutEvent>(OnMouseOut);
            UnregisterCallback<MouseDownEvent>(OnMouseDown);
            UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);

            _collapseButton?.UnregisterCallback<MouseDownEvent>(OnCollapseButtonMouseDown);
            _collapseButton?.UnregisterCallback<MouseOverEvent>(OnCollapseButtonMouseOver);
            _collapseButton?.UnregisterCallback<MouseOutEvent>(OnCollapseButtonMouseOut);

            Unbind();
        }

        private void OnMouseOver(MouseOverEvent evt) {
            if (NodeGraphPort.IsDragging) return;
            _data.InteractionState |= InteractionState.Hovered;
        }

        private void OnMouseOut(MouseOutEvent evt) {
            _data.InteractionState &= ~InteractionState.Hovered;
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0 || (evt.button == 1 && !evt.altKey)) {
                BringToFront();
                var e = this.GetPooled<NodeClickEvent>();
                e.Node = _data.Entity;
                e.ShiftKey = evt.shiftKey;
                this.Send(e);
            }

            if (evt.button == 0) {
                _dragging = true;
                _moved = false;
                _startMousePosition = evt.mousePosition;
                this.Send<StartDragNodesEvent>();
                this.CaptureMouse();
                evt.StopPropagation();
            }
            else if (evt.button == 1 && !evt.altKey) {
                var e = this.GetPooled<NodeRightClickEvent>();
                e.Node = _data.Entity;
                e.MousePosition = evt.localMousePosition;
                this.Send(e);
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (!_dragging) return;

            Vector2 delta = evt.mousePosition - _startMousePosition;

            if (!_moved && delta.sqrMagnitude > 1e-3f) {
                Undo.Record();
                _moved = true;
            }

            var e = this.GetPooled<DragNodesEvent>();
            e.Node = this;
            e.Delta = delta;
            this.Send(e);

            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (!_dragging) return;

            _dragging = false;
            this.Send<EndDragNodesEvent>();
            this.ReleaseMouse();
            evt.StopPropagation();
        }

        private void OnCollapseButtonMouseOver(MouseOverEvent evt) {
            _collapseButton.style.backgroundColor = s_HoverColor;
            evt.StopPropagation();
        }

        private void OnCollapseButtonMouseOut(MouseOutEvent evt) {
            _collapseButton.style.backgroundColor = Color.clear;
            evt.StopPropagation();
        }

        private void OnCollapseButtonMouseDown(MouseDownEvent evt) {
            if (evt.button != 0) return;

            bool collapsed = _itemsContainer.style.display == DisplayStyle.None;
            collapsed = !collapsed;
            _itemsContainer.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _foldout.style.width = collapsed ? 24f : 168f;
            _collapseButton.text = collapsed ? "►" : "◄";

            evt.StopPropagation();
        }
    }
}
