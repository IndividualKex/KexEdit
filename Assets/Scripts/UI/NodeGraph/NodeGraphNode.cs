using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    public class NodeGraphNode : VisualElement {
        private VisualElement _header;
        private VisualElement _headerDivider;
        private VisualElement _contents;
        private VisualElement _inputsContainer;
        private VisualElement _portsDivider;
        private VisualElement _outputsContainer;
        private DurationTypeField _durationTypeField;
        private RenderToggle _renderToggle;
        private PriorityField _priorityField;
        private VisualElement _footerDivider;
        private VisualElement _footer;
        private VisualElement _itemsContainer;
        private Label _collapseButton;

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

            _header = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexGrow = 1f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    backgroundColor = s_AltBackgroundColor
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
            Add(_header);

            _headerDivider = new VisualElement {
                style = {
                    position = Position.Relative,
                    height = 1f,
                    flexGrow = 1f,
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
            Add(_headerDivider);

            _contents = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexGrow = 1f,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Stretch,
                    justifyContent = Justify.FlexEnd,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                }
            };
            Add(_contents);

            _inputsContainer = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.FlexStart,
                    alignItems = Align.FlexStart,
                    backgroundColor = s_AltBackgroundColor,
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
            _contents.Add(_inputsContainer);

            _portsDivider = new VisualElement {
                style = {
                    position = Position.Relative,
                    width = 1f,
                    backgroundColor = s_DividerColor,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f
                }
            };
            _contents.Add(_portsDivider);

            _outputsContainer = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.FlexStart,
                    alignItems = Align.FlexEnd,
                    backgroundColor = s_AltBackgroundColor,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    marginLeft = 0f,
                    marginRight = 0f,
                }
            };
            _contents.Add(_outputsContainer);

            _footerDivider = new VisualElement {
                style = {
                    position = Position.Relative,
                    height = 1f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    backgroundColor = s_DividerColor
                }
            };
            Add(_footerDivider);

            _footer = new VisualElement {
                style = {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.Stretch,
                    minHeight = 12f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    backgroundColor = s_AltBackgroundColor
                }
            };
            Add(_footer);

            if (_type == NodeType.ForceSection
                || _type == NodeType.GeometricSection
                || _type == NodeType.CurvedSection
                || _type == NodeType.CopyPathSection
                || _type == NodeType.Reverse
                || _type == NodeType.ReversePath
                || _type == NodeType.Mesh) {
                _itemsContainer = new VisualElement {
                    style = {
                        position = Position.Relative,
                        flexDirection = FlexDirection.Column,
                        alignItems = Align.Stretch,
                        justifyContent = Justify.FlexStart,
                        paddingLeft = 0f,
                        paddingRight = 0f,
                        paddingTop = 4f,
                        paddingBottom = 4f,
                        marginLeft = 0f,
                        marginRight = 0f,
                        marginTop = 0f,
                        marginBottom = 0f,
                        backgroundColor = s_AltBackgroundColor,
                        display = DisplayStyle.None
                    }
                };
                _footer.Add(_itemsContainer);

                if (_type == NodeType.ForceSection
                    || _type == NodeType.GeometricSection) {
                    _durationTypeField = new DurationTypeField();
                    _itemsContainer.Add(_durationTypeField);
                }

                if (_type == NodeType.ForceSection
                    || _type == NodeType.GeometricSection
                    || _type == NodeType.CurvedSection
                    || _type == NodeType.CopyPathSection
                    || _type == NodeType.Mesh) {
                    _renderToggle = new RenderToggle();
                    _itemsContainer.Add(_renderToggle);
                }

                if (_type != NodeType.Mesh && _type != NodeType.Append) {
                    _priorityField = new PriorityField();
                    _itemsContainer.Add(_priorityField);
                }

                _collapseButton = new Label("▼") {
                    style = {
                        position = Position.Relative,
                        paddingLeft = 0f,
                        paddingRight = 0f,
                        paddingTop = 3f,
                        paddingBottom = 3f,
                        marginLeft = 0f,
                        marginRight = 0f,
                        marginTop = 0f,
                        marginBottom = 0f,
                        backgroundColor = Color.clear,
                        fontSize = 9f,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                _footer.Add(_collapseButton);
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

            foreach (var portData in _data.Inputs.Values) {
                var port = new NodeGraphPort(_view, portData);
                _ports.Add(portData.Entity, port);
                _inputsContainer.Add(port);
            }

            foreach (var portData in _data.Outputs.Values) {
                var port = new NodeGraphPort(_view, portData);
                _ports.Add(portData.Entity, port);
                _outputsContainer.Add(port);
            }

            SetBinding("style.borderTopColor", _interactionStateBinding);
            SetBinding("style.borderBottomColor", _interactionStateBinding);
            SetBinding("style.borderLeftColor", _interactionStateBinding);
            SetBinding("style.borderRightColor", _interactionStateBinding);
            SetBinding("style.left", _leftBinding);
            SetBinding("style.top", _topBinding);
        }

        private void Unbind() {
            _data = null;
            dataSource = null;

            _durationTypeField?.Unbind();
            _renderToggle?.Unbind();
            _priorityField?.Unbind();

            _inputsContainer.Clear();
            _outputsContainer.Clear();
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
            _collapseButton.text = collapsed ? "▼" : "▲";

            evt.StopPropagation();
        }
    }
}
