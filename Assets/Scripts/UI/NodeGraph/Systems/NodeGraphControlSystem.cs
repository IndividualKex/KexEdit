using System;
using System.Collections.Generic;
using KexEdit.UI.Serialization;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.Constants;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class NodeGraphControlSystem : SystemBase, IEditableHandler {
        private byte[] _clipboardData = null;
        private float2 _clipboardCenter = float2.zero;
        private float2 _clipboardPan = float2.zero;
        private NodeGraphData _data;
        private NodeGraphView _view;

        private EntityQuery _nodeQuery;
        private EntityQuery _connectionQuery;
        private EntityQuery _portQuery;

        protected override void OnCreate() {
            _data = new NodeGraphData();

            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<NodeAspect>()
                .Build(EntityManager);
            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<ConnectionAspect>()
                .Build(EntityManager);
            _portQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Port>()
                .Build(EntityManager);
        }

        protected override void OnStartRunning() {
            var root = UIService.Instance.UIDocument.rootVisualElement;
            _view = root.Q<NodeGraphView>();
            _view.Initialize(_data);

            _view.RegisterCallback<ViewRightClickEvent>(OnViewRightClick);
            _view.RegisterCallback<NodeClickEvent>(OnNodeClick);
            _view.RegisterCallback<NodeRightClickEvent>(OnNodeRightClick);
            _view.RegisterCallback<EdgeClickEvent>(OnEdgeClick);
            _view.RegisterCallback<EdgeRightClickEvent>(OnEdgeRightClick);
            _view.RegisterCallback<StartDragNodesEvent>(OnStartDragNodes);
            _view.RegisterCallback<DragNodesEvent>(OnDragNodes);
            _view.RegisterCallback<EndDragNodesEvent>(OnEndDragNodes);
            _view.RegisterCallback<PortChangeEvent>(OnPortChange);
            _view.RegisterCallback<DragOutputPortEvent>(OnDragOutputPort);
            _view.RegisterCallback<AnchorPromoteEvent>(OnAnchorPromote);
            _view.RegisterCallback<AddConnectionEvent>(OnAddConnection);
            _view.RegisterCallback<SelectionEvent>(OnSelection);
            _view.RegisterCallback<ClearSelectionEvent>(_ => ClearSelection());
            _view.RegisterCallback<DurationTypeChangeEvent>(OnDurationTypeChange);
            _view.RegisterCallback<RenderToggleChangeEvent>(OnRenderToggleChange);
            _view.RegisterCallback<PriorityChangeEvent>(OnPriorityChange);

            EditOperationsSystem.RegisterHandler(this);
        }

        protected override void OnDestroy() {
            EditOperationsSystem.UnregisterHandler(this);
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            InitializeNodes();
            InitializeEdges();
            UpdateNodes();
            UpdateEdges();
            _view.Draw();
        }

        private void InitializeNodes() {
            using var entities = _nodeQuery.ToEntityArray(Allocator.Temp);
            using var set = new NativeHashSet<Entity>(entities.Length, Allocator.Temp);
            foreach (var entity in entities) {
                set.Add(entity);

                if (_data.Nodes.ContainsKey(entity)) continue;

                DurationType durationType = DurationType.Time;
                if (SystemAPI.HasComponent<Duration>(entity)) {
                    durationType = SystemAPI.GetComponent<Duration>(entity).Type;
                }

                bool render = false;
                if (SystemAPI.HasComponent<Render>(entity)) {
                    render = SystemAPI.GetComponent<Render>(entity);
                }

                var node = SystemAPI.GetAspect<NodeAspect>(entity);
                var nodeData = NodeData.Create(node, durationType, render);

                var inputPortReferences = SystemAPI.GetBuffer<InputPortReference>(entity);
                foreach (var inputPortReference in inputPortReferences) {
                    var portEntity = inputPortReference.Value;
                    var port = SystemAPI.GetComponent<Port>(portEntity);
                    UnitsType units = GetUnits(port.Type, durationType);
                    var portData = PortData.Create(portEntity, port, entity, units);
                    nodeData.OrderedInputs.Add(portEntity);
                    nodeData.Inputs.Add(portEntity, portData);
                }

                var outputPortReferences = SystemAPI.GetBuffer<OutputPortReference>(entity);
                foreach (var outputPortReference in outputPortReferences) {
                    var portEntity = outputPortReference.Value;
                    var port = SystemAPI.GetComponent<Port>(portEntity);
                    UnitsType units = GetUnits(port.Type, durationType);
                    var portData = PortData.Create(portEntity, port, entity, units);
                    nodeData.OrderedOutputs.Add(portEntity);
                    nodeData.Outputs.Add(portEntity, portData);
                }

                _data.Nodes.Add(entity, nodeData);
            }

            using var toRemove = new NativeList<Entity>(Allocator.Temp);
            foreach (var (entity, nodeData) in _data.Nodes) {
                if (set.Contains(entity) && SystemAPI.Exists(entity)) continue;
                toRemove.Add(entity);
            }
            foreach (var entity in toRemove) {
                _data.Nodes.Remove(entity);
            }
        }

        private UnitsType GetUnits(PortType portType, DurationType durationType) {
            return portType switch {
                PortType.Duration => durationType switch {
                    DurationType.Time => UnitsType.Time,
                    DurationType.Distance => UnitsType.Distance,
                    _ => throw new NotImplementedException(),
                },
                _ => portType.GetUnits(),
            };
        }

        private void InitializeEdges() {
            using var entities = _connectionQuery.ToEntityArray(Allocator.Temp);
            using var set = new NativeHashSet<Entity>(entities.Length, Allocator.Temp);
            foreach (var entity in entities) {
                var connection = SystemAPI.GetAspect<ConnectionAspect>(entity);
                set.Add(entity);

                if (_data.Edges.ContainsKey(entity)) continue;

                var edgeData = EdgeData.Create(connection);
                _data.Edges.Add(entity, edgeData);
            }

            using var toRemove = new NativeList<Entity>(Allocator.Temp);
            foreach (var (entity, edgeData) in _data.Edges) {
                if (set.Contains(entity) && SystemAPI.Exists(entity)) continue;
                toRemove.Add(entity);
            }
            foreach (var entity in toRemove) {
                _data.Edges.Remove(entity);
            }
        }

        private void UpdateNodes() {
            using var connections = _connectionQuery.ToComponentDataArray<Connection>(Allocator.Temp);
            using var connected = new NativeHashSet<Entity>(connections.Length * 2, Allocator.Temp);
            foreach (var connection in connections) {
                connected.Add(connection.Source);
                connected.Add(connection.Target);
            }

            _data.HasSelectedNodes = false;
            foreach (var nodeData in _data.Nodes.Values) {
                Entity entity = nodeData.Entity;
                var node = SystemAPI.GetAspect<NodeAspect>(entity);

                DurationType durationType = DurationType.Time;
                if (SystemAPI.HasComponent<Duration>(entity)) {
                    durationType = SystemAPI.GetComponent<Duration>(entity).Type;
                }

                bool render = false;
                if (SystemAPI.HasComponent<Render>(entity)) {
                    render = SystemAPI.GetComponent<Render>(entity);
                }

                nodeData.Update(node, durationType, render);

                foreach (var portData in nodeData.Inputs.Values) {
                    UpdateInputPortValue(portData);

                    if (portData.Port.Type == PortType.Duration) {
                        portData.Units = durationType switch {
                            DurationType.Time => UnitsType.Time,
                            DurationType.Distance => UnitsType.Distance,
                            _ => throw new NotImplementedException(),
                        };
                    }

                    bool isConnected = connected.Contains(portData.Entity);
                    portData.Update(isConnected);
                }

                foreach (var portData in nodeData.Outputs.Values) {
                    switch (portData.Port.Type) {
                        case PortType.Anchor:
                            PointData anchorValue = SystemAPI.GetComponent<AnchorPort>(portData.Entity);
                            portData.SetValue(anchorValue);
                            break;
                        case PortType.Path:
                            // Pass
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    if (portData.Port.Type == PortType.Duration) {
                        portData.Units = durationType switch {
                            DurationType.Time => UnitsType.Time,
                            DurationType.Distance => UnitsType.Distance,
                            _ => throw new NotImplementedException(),
                        };
                    }

                    bool isConnected = connected.Contains(portData.Entity);
                    portData.Update(isConnected);
                }

                _data.HasSelectedNodes |= node.Selected;
            }
        }

        private void UpdateEdges() {
            _data.HasSelectedEdges = false;

            foreach (var edgeData in _data.Edges.Values) {
                var connection = SystemAPI.GetAspect<ConnectionAspect>(edgeData.Entity);
                edgeData.Update(connection);

                _data.HasSelectedEdges |= connection.Selected;
            }
        }

        private void OnViewRightClick(ViewRightClickEvent evt) {
            (evt.target as VisualElement).ShowContextMenu(evt.MousePosition, menu => {
                menu.AddItem("Force Section", () => {
                    Undo.Record();
                    AddNode(evt.ContentPosition, NodeType.ForceSection);
                });
                menu.AddItem("Geometric Section", () => {
                    Undo.Record();
                    AddNode(evt.ContentPosition, NodeType.GeometricSection);
                });
                menu.AddItem("Curved Section", () => {
                    Undo.Record();
                    AddNode(evt.ContentPosition, NodeType.CurvedSection);
                });
                menu.AddSubmenu("More", submenu => {
                    submenu.AddItem("Copy Path Section", () => {
                        Undo.Record();
                        AddNode(evt.ContentPosition, NodeType.CopyPathSection);
                    });
                    submenu.AddItem("Anchor", () => {
                        Undo.Record();
                        AddNode(evt.ContentPosition, NodeType.Anchor);
                    });
                    submenu.AddItem("Bridge", () => {
                        Undo.Record();
                        AddNode(evt.ContentPosition, NodeType.Bridge);
                    });
                    submenu.AddItem("Reverse", () => {
                        Undo.Record();
                        AddNode(evt.ContentPosition, NodeType.Reverse);
                    });
                    submenu.AddItem("Reverse Path", () => {
                        Undo.Record();
                        AddNode(evt.ContentPosition, NodeType.ReversePath);
                    });
                    submenu.AddSeparator();
                    submenu.AddItem("Mesh", () => {
                        ImportManager.ShowGltfImportDialog(_view, filePath => {
                            Undo.Record();
                            var node = AddNode(evt.ContentPosition, NodeType.Mesh);
                            EntityManager.AddComponentData<MeshReference>(node, new MeshReference {
                                FilePath = filePath,
                                Value = null,
                                Loaded = false
                            });
                        });
                    });
                });
                menu.AddSeparator();
                var canPaste = EditOperationsSystem.CanPaste;
                menu.AddItem(canPaste ? "Paste" : "Cannot Paste", () => {
                    if (EditOperationsSystem.CanPaste) {
                        EditOperationsSystem.HandlePaste(evt.MousePosition);
                    }
                }, "Ctrl+V".ToPlatformShortcut(), enabled: canPaste);
            });
        }

        private void OnNodeClick(NodeClickEvent evt) {
            var nodeData = _data.Nodes[evt.Node];
            if (evt.ShiftKey) {
                if (nodeData.Selected) {
                    DeselectNode(nodeData);
                }
                else {
                    SelectNode(nodeData, true);
                }
            }
            else {
                SelectNode(nodeData, false);
            }
        }

        private void OnNodeRightClick(NodeRightClickEvent evt) {
            _data.HasSelectedNodes = true;
            var nodeData = _data.Nodes[evt.Node];

            (evt.target as VisualElement).ShowContextMenu(evt.MousePosition, menu => {
                bool canCut = EditOperationsSystem.CanCut;
                bool canCopy = EditOperationsSystem.CanCopy;
                bool canPaste = EditOperationsSystem.CanPaste;

                if (nodeData.Type == NodeType.Mesh) {
                    menu.AddItem("Link", () => {
                        Undo.Record();
                        LinkMesh(nodeData);
                    });
                    menu.AddSeparator();
                }

                menu.AddPlatformItem(canCut ? "Cut" : "Cannot Cut", EditOperationsSystem.HandleCut, "Ctrl+X", enabled: canCut);
                menu.AddPlatformItem(canCopy ? "Copy" : "Cannot Copy", EditOperationsSystem.HandleCopy, "Ctrl+C", enabled: canCopy);
                menu.AddPlatformItem(canPaste ? "Paste" : "Cannot Paste", EditOperationsSystem.HandlePaste, "Ctrl+V", enabled: canPaste);
                menu.AddSeparator();
                menu.AddItem("Delete", () => {
                    Undo.Record();
                    RemoveSelected();
                }, "Del");
            });
        }

        private void OnEdgeClick(EdgeClickEvent evt) {
            var edgeData = _data.Edges[evt.Edge];
            if (evt.ShiftKey) {
                if (edgeData.Selected) {
                    DeselectEdge(edgeData);
                }
                else {
                    SelectEdge(edgeData, true);
                }
            }
            else {
                SelectEdge(edgeData, false);
            }
        }

        private void OnEdgeRightClick(EdgeRightClickEvent evt) {
            _data.HasSelectedEdges = true;

            (evt.target as VisualElement).ShowContextMenu(evt.MousePosition, menu => {
                menu.AddItem("Delete", () => {
                    Undo.Record();
                    RemoveSelected();
                });
            });
        }

        private void OnStartDragNodes(StartDragNodesEvent evt) {
            foreach (var nodeData in _data.Nodes.Values) {
                nodeData.DragStartPosition = nodeData.Position;
            }
        }

        private void OnDragNodes(DragNodesEvent evt) {
            float2 position = evt.Node.Data.DragStartPosition + (float2)evt.Delta / _data.Zoom;
            position = _view.SnapNodePosition(evt.Node, position);
            float2 delta = position - evt.Node.Data.DragStartPosition;

            foreach (var nodeData in _data.Nodes.Values) {
                ref Node node = ref SystemAPI.GetComponentRW<Node>(nodeData.Entity).ValueRW;
                if (!node.Selected) continue;
                node.Position = nodeData.DragStartPosition + delta;
            }
        }

        private void OnEndDragNodes(EndDragNodesEvent evt) {
            _view.ClearGuides();
        }

        private void OnPortChange(PortChangeEvent evt) {
            ApplyInputPortValue(evt.Port);
        }

        private void OnDragOutputPort(DragOutputPortEvent evt) {
            if (evt.Port.Port.Type == PortType.Anchor) {
                Vector2 localPosition = evt.MousePosition;
                Vector2 worldPosition = _view.LocalToWorld(localPosition);
                Vector2 viewPosition = _view.WorldToLocal(worldPosition);
                Vector2 contentPosition = (viewPosition - _data.Pan) / _data.Zoom;
                _view.ShowContextMenu(viewPosition, menu => {
                    menu.AddItem("Force Section", () => {
                        Undo.Record();
                        AddConnectedNode(evt.Port, contentPosition, NodeType.ForceSection, 0);
                    });
                    menu.AddItem("Geometric Section", () => {
                        Undo.Record();
                        AddConnectedNode(evt.Port, contentPosition, NodeType.GeometricSection, 0);
                    });
                    menu.AddItem("Curved Section", () => {
                        Undo.Record();
                        AddConnectedNode(evt.Port, contentPosition, NodeType.CurvedSection, 0);
                    });
                    menu.AddItem("Copy Path Section", () => {
                        Undo.Record();
                        AddConnectedNode(evt.Port, contentPosition, NodeType.CopyPathSection, 0);
                    });
                    menu.AddItem("Bridge", () => {
                        Undo.Record();
                        AddConnectedNode(evt.Port, contentPosition, NodeType.Bridge, 0);
                    });
                    menu.AddItem("Reverse", () => {
                        Undo.Record();
                        AddConnectedNode(evt.Port, contentPosition, NodeType.Reverse, 0);
                    });
                });
            }
            else if (evt.Port.Port.Type == PortType.Path) {
                Vector2 localPosition = evt.MousePosition;
                Vector2 worldPosition = _view.LocalToWorld(localPosition);
                Vector2 viewPosition = _view.WorldToLocal(worldPosition);
                Vector2 contentPosition = (viewPosition - _data.Pan) / _data.Zoom;
                _view.ShowContextMenu(viewPosition, menu => {
                    menu.AddItem("Copy Path Section", () => {
                        Undo.Record();
                        AddConnectedNode(evt.Port, contentPosition, NodeType.CopyPathSection, 1);
                    });
                    menu.AddItem("Reverse Path", () => {
                        Undo.Record();
                        AddConnectedNode(evt.Port, contentPosition, NodeType.ReversePath, 0);
                    });
                });
            }
        }

        private void OnAnchorPromote(AnchorPromoteEvent evt) {
            if (!evt.Port.Port.IsInput || evt.Port.Port.Type != PortType.Anchor) {
                throw new NotImplementedException("Only input anchor ports can be promoted");
            }

            var targetNode = _data.Nodes[evt.Port.Node];

            float2 sourcePosition = targetNode.Position + new float2(-350f, 0f);
            var node = AddNode(sourcePosition, NodeType.Anchor);

            var sourcePort = SystemAPI.GetBuffer<OutputPortReference>(node)[0];
            var targetPort = evt.Port.Entity;
            AddConnection(sourcePort, targetPort);

            PointData anchor = SystemAPI.GetComponent<AnchorPort>(targetPort).Value;
            ref var nodeAnchor = ref SystemAPI.GetComponentRW<Anchor>(node).ValueRW;
            nodeAnchor.Value = anchor;

            var anchorInputBuffer = SystemAPI.GetBuffer<InputPortReference>(node);

            ref var positionPort = ref SystemAPI.GetComponentRW<PositionPort>(anchorInputBuffer[0].Value).ValueRW;
            positionPort.Value = anchor.Position;

            ref var rollPort = ref SystemAPI.GetComponentRW<RollPort>(anchorInputBuffer[1].Value).ValueRW;
            rollPort.Value = anchor.Roll;

            ref var pitchPort = ref SystemAPI.GetComponentRW<PitchPort>(anchorInputBuffer[2].Value).ValueRW;
            pitchPort.Value = anchor.GetPitch();

            ref var yawPort = ref SystemAPI.GetComponentRW<YawPort>(anchorInputBuffer[3].Value).ValueRW;
            yawPort.Value = anchor.GetYaw();

            ref var velocityPortRW = ref SystemAPI.GetComponentRW<VelocityPort>(anchorInputBuffer[4].Value).ValueRW;
            velocityPortRW.Value = anchor.Velocity;

            ref var heartPortRW = ref SystemAPI.GetComponentRW<HeartPort>(anchorInputBuffer[5].Value).ValueRW;
            heartPortRW.Value = anchor.Heart;

            ref var frictionPortRW = ref SystemAPI.GetComponentRW<FrictionPort>(anchorInputBuffer[6].Value).ValueRW;
            frictionPortRW.Value = anchor.Friction;

            ref var resistancePortRW = ref SystemAPI.GetComponentRW<ResistancePort>(anchorInputBuffer[7].Value).ValueRW;
            resistancePortRW.Value = anchor.Resistance;
        }

        private void OnAddConnection(AddConnectionEvent evt) {
            Entity source = evt.Source.Entity;
            Entity target = evt.Target.Entity;
            AddConnection(source, target);
        }

        private void OnSelection(SelectionEvent evt) {
            if (evt.Nodes != null) {
                foreach (var entity in evt.Nodes) {
                    ref Node node = ref SystemAPI.GetComponentRW<Node>(entity).ValueRW;
                    node.Selected = true;
                }
            }
            if (evt.Edges != null) {
                foreach (var entity in evt.Edges) {
                    ref Connection connection = ref SystemAPI.GetComponentRW<Connection>(entity).ValueRW;
                    connection.Selected = true;
                }
            }

            UpdateSelectionState();
        }

        private void OnDurationTypeChange(DurationTypeChangeEvent evt) {
            ref var duration = ref SystemAPI.GetComponentRW<Duration>(evt.Node).ValueRW;
            duration.Type = evt.DurationType;

            ref var dirty = ref SystemAPI.GetComponentRW<Dirty>(evt.Node).ValueRW;
            dirty = true;
        }

        private void OnRenderToggleChange(RenderToggleChangeEvent evt) {
            ref var render = ref SystemAPI.GetComponentRW<Render>(evt.Node).ValueRW;
            render.Value = evt.Render;
        }

        private void OnPriorityChange(PriorityChangeEvent evt) {
            ref var node = ref SystemAPI.GetComponentRW<Node>(evt.Node).ValueRW;
            node.Priority = evt.Priority;
        }

        public void SelectAll() {
            foreach (var nodeData in _data.Nodes.Values) {
                ref Node node = ref SystemAPI.GetComponentRW<Node>(nodeData.Entity).ValueRW;
                node.Selected = true;
            }
            foreach (var edgeData in _data.Edges.Values) {
                ref Connection connection = ref SystemAPI.GetComponentRW<Connection>(edgeData.Entity).ValueRW;
                connection.Selected = true;
            }

            UpdateSelectionState();
        }

        private void ClearSelection() {
            foreach (var nodeData in _data.Nodes.Values) {
                ref Node node = ref SystemAPI.GetComponentRW<Node>(nodeData.Entity).ValueRW;
                node.Selected = false;
            }
            foreach (var edgeData in _data.Edges.Values) {
                ref Connection connection = ref SystemAPI.GetComponentRW<Connection>(edgeData.Entity).ValueRW;
                connection.Selected = false;
            }

            _data.HasSelectedNodes = false;
            _data.HasSelectedEdges = false;
        }

        private void AddConnection(Entity source, Entity target) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var connections = _connectionQuery.ToEntityArray(Allocator.Temp);
            using var toRemove = new NativeHashSet<Entity>(connections.Length, Allocator.Temp);
            foreach (var entity in connections) {
                var existing = SystemAPI.GetComponent<Connection>(entity);
                if (existing.Target == target) {
                    toRemove.Add(entity);
                }
            }
            foreach (var entity in toRemove) {
                ecb.DestroyEntity(entity);
            }

            var connection = ecb.CreateEntity();
            ecb.AddComponent<Dirty>(connection);
            ecb.AddComponent(connection, Connection.Create(source, target, false));
            ecb.SetName(connection, "Connection");

            ecb.SetComponent<Dirty>(source, true);

            ecb.Playback(EntityManager);
        }

        private void SelectNode(NodeData data, bool addToSelection) {
            if (!data.Selected && !addToSelection) {
                ClearSelection();
            }

            ref Node node = ref SystemAPI.GetComponentRW<Node>(data.Entity).ValueRW;
            if (node.Selected && addToSelection) {
                node.Selected = false;
            }
            else {
                node.Selected = true;
            }

            UpdateSelectionState();
        }

        private void DeselectNode(NodeData data) {
            ref Node node = ref SystemAPI.GetComponentRW<Node>(data.Entity).ValueRW;
            node.Selected = false;
            UpdateSelectionState();
        }

        private void SelectEdge(EdgeData data, bool addToSelection) {
            if (!data.Selected && !addToSelection) {
                ClearSelection();
            }

            ref Connection connection = ref SystemAPI.GetComponentRW<Connection>(data.Entity).ValueRW;
            if (connection.Selected && addToSelection) {
                connection.Selected = false;
            }
            else {
                connection.Selected = true;
            }

            UpdateSelectionState();
        }

        private void DeselectEdge(EdgeData data) {
            ref Connection connection = ref SystemAPI.GetComponentRW<Connection>(data.Entity).ValueRW;
            connection.Selected = false;
            UpdateSelectionState();
        }

        private void RemoveSelected() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var node in _data.Nodes.Values) {
                if (!node.Selected) continue;
                var entity = node.Entity;
                var inputPortBuffer = SystemAPI.GetBuffer<InputPortReference>(entity);
                var outputPortBuffer = SystemAPI.GetBuffer<OutputPortReference>(entity);
                foreach (var port in inputPortBuffer) {
                    ecb.DestroyEntity(port);
                }
                foreach (var port in outputPortBuffer) {
                    ecb.DestroyEntity(port);
                }
                ecb.DestroyEntity(entity);
            }
            foreach (var edge in _data.Edges.Values) {
                if (!edge.Selected) continue;
                var entity = edge.Entity;
                ecb.DestroyEntity(entity);
            }
            ecb.Playback(EntityManager);

            UpdateSelectionState();
        }

        private void UpdateSelectionState() {
            _data.HasSelectedNodes = false;
            foreach (var node in _data.Nodes.Values) {
                if (!node.Selected) continue;
                _data.HasSelectedNodes = true;
                break;
            }
            foreach (var edge in _data.Edges.Values) {
                if (!edge.Selected) continue;
                _data.HasSelectedEdges = true;
                break;
            }
        }

        private void LinkMesh(NodeData nodeData) {
            var meshReference = SystemAPI.ManagedAPI.GetComponent<MeshReference>(nodeData.Entity);
            ImportManager.ShowGltfImportDialog(_view, filePath => {
                Undo.Record();
                meshReference.FilePath = filePath;
                meshReference.Value = null;
                meshReference.Loaded = false;
            });
        }

        private void UpdateInputPortValue(PortData port) {
            switch (port.Port.Type) {
                case PortType.Anchor:
                    PointData anchorValue = SystemAPI.GetComponent<AnchorPort>(port.Entity);
                    port.SetValue(anchorValue);
                    break;
                case PortType.Path:
                    // Pass
                    break;
                case PortType.Duration:
                    float durationValue = SystemAPI.GetComponent<DurationPort>(port.Entity);
                    port.SetValue(durationValue);
                    break;
                case PortType.Position:
                    float3 positionValue = SystemAPI.GetComponent<PositionPort>(port.Entity);
                    port.SetValue(positionValue);
                    break;
                case PortType.Roll:
                    float rollValue = SystemAPI.GetComponent<RollPort>(port.Entity);
                    port.SetValue(rollValue);
                    break;
                case PortType.Pitch:
                    float pitchValue = SystemAPI.GetComponent<PitchPort>(port.Entity);
                    port.SetValue(pitchValue);
                    break;
                case PortType.Yaw:
                    float yawValue = SystemAPI.GetComponent<YawPort>(port.Entity);
                    port.SetValue(yawValue);
                    break;
                case PortType.Velocity:
                    float velocityValue = SystemAPI.GetComponent<VelocityPort>(port.Entity);
                    port.SetValue(velocityValue);
                    break;
                case PortType.Heart:
                    float heartValue = SystemAPI.GetComponent<HeartPort>(port.Entity);
                    port.SetValue(heartValue);
                    break;
                case PortType.Friction:
                    float frictionPhysicsValue = SystemAPI.GetComponent<FrictionPort>(port.Entity);
                    float frictionUIValue = frictionPhysicsValue * FRICTION_PHYSICS_TO_UI_SCALE;
                    port.SetValue(frictionUIValue);
                    break;
                case PortType.Resistance:
                    float resistancePhysicsValue = SystemAPI.GetComponent<ResistancePort>(port.Entity);
                    float resistanceUIValue = resistancePhysicsValue * RESISTANCE_PHYSICS_TO_UI_SCALE;
                    port.SetValue(resistanceUIValue);
                    break;
                case PortType.Radius:
                    float radiusValue = SystemAPI.GetComponent<RadiusPort>(port.Entity);
                    port.SetValue(radiusValue);
                    break;
                case PortType.Arc:
                    float arcValue = SystemAPI.GetComponent<ArcPort>(port.Entity);
                    port.SetValue(arcValue);
                    break;
                case PortType.Axis:
                    float axisValue = SystemAPI.GetComponent<AxisPort>(port.Entity);
                    port.SetValue(axisValue);
                    break;
                case PortType.LeadIn:
                    float leadInValue = SystemAPI.GetComponent<LeadInPort>(port.Entity);
                    port.SetValue(leadInValue);
                    break;
                case PortType.LeadOut:
                    float leadOutValue = SystemAPI.GetComponent<LeadOutPort>(port.Entity);
                    port.SetValue(leadOutValue);
                    break;
                case PortType.Rotation:
                    float3 rotationValue = SystemAPI.GetComponent<RotationPort>(port.Entity);
                    port.SetValue(rotationValue);
                    break;
                case PortType.Scale:
                    float scaleValue = SystemAPI.GetComponent<ScalePort>(port.Entity);
                    port.SetValue(scaleValue);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void ApplyInputPortValue(PortData port) {
            switch (port.Port.Type) {
                case PortType.Anchor:
                    port.GetValue(out PointData anchorValue);
                    ref var anchor = ref SystemAPI.GetComponentRW<AnchorPort>(port.Entity).ValueRW;
                    anchor.Value = anchorValue;
                    break;
                case PortType.Path:
                    // Pass
                    break;
                case PortType.Duration:
                    port.GetValue(out float durationValue);
                    ref var duration = ref SystemAPI.GetComponentRW<DurationPort>(port.Entity).ValueRW;
                    duration.Value = durationValue;
                    break;
                case PortType.Position:
                    port.GetValue(out float3 positionValue);
                    ref var position = ref SystemAPI.GetComponentRW<PositionPort>(port.Entity).ValueRW;
                    position.Value = positionValue;
                    break;
                case PortType.Roll:
                    port.GetValue(out float rollValue);
                    ref var roll = ref SystemAPI.GetComponentRW<RollPort>(port.Entity).ValueRW;
                    roll.Value = rollValue;
                    break;
                case PortType.Pitch:
                    port.GetValue(out float pitchValue);
                    ref var pitch = ref SystemAPI.GetComponentRW<PitchPort>(port.Entity).ValueRW;
                    pitch.Value = pitchValue;
                    break;
                case PortType.Yaw:
                    port.GetValue(out float yawValue);
                    ref var yaw = ref SystemAPI.GetComponentRW<YawPort>(port.Entity).ValueRW;
                    yaw.Value = yawValue;
                    break;
                case PortType.Velocity:
                    port.GetValue(out float velocityValue);
                    ref var velocity = ref SystemAPI.GetComponentRW<VelocityPort>(port.Entity).ValueRW;
                    velocity.Value = velocityValue;
                    break;
                case PortType.Heart:
                    port.GetValue(out float heartValue);
                    ref var heart = ref SystemAPI.GetComponentRW<HeartPort>(port.Entity).ValueRW;
                    heart.Value = heartValue;
                    break;
                case PortType.Friction:
                    port.GetValue(out float frictionUIValue);
                    float frictionPhysicsValue = frictionUIValue * FRICTION_UI_TO_PHYSICS_SCALE;
                    ref var friction = ref SystemAPI.GetComponentRW<FrictionPort>(port.Entity).ValueRW;
                    friction.Value = frictionPhysicsValue;
                    break;
                case PortType.Resistance:
                    port.GetValue(out float resistanceUIValue);
                    float resistancePhysicsValue = resistanceUIValue * RESISTANCE_UI_TO_PHYSICS_SCALE;
                    ref var resistance = ref SystemAPI.GetComponentRW<ResistancePort>(port.Entity).ValueRW;
                    resistance.Value = resistancePhysicsValue;
                    break;
                case PortType.Radius:
                    port.GetValue(out float radiusValue);
                    ref var radius = ref SystemAPI.GetComponentRW<RadiusPort>(port.Entity).ValueRW;
                    radius.Value = radiusValue;
                    break;
                case PortType.Arc:
                    port.GetValue(out float arcValue);
                    ref var arc = ref SystemAPI.GetComponentRW<ArcPort>(port.Entity).ValueRW;
                    arc.Value = arcValue;
                    break;
                case PortType.Axis:
                    port.GetValue(out float axisValue);
                    ref var axis = ref SystemAPI.GetComponentRW<AxisPort>(port.Entity).ValueRW;
                    axis.Value = axisValue;
                    break;
                case PortType.LeadIn:
                    port.GetValue(out float leadInValue);
                    ref var leadIn = ref SystemAPI.GetComponentRW<LeadInPort>(port.Entity).ValueRW;
                    leadIn.Value = leadInValue;
                    break;
                case PortType.LeadOut:
                    port.GetValue(out float leadOutValue);
                    ref var leadOut = ref SystemAPI.GetComponentRW<LeadOutPort>(port.Entity).ValueRW;
                    leadOut.Value = leadOutValue;
                    break;
                case PortType.Rotation:
                    port.GetValue(out float3 rotationValue);
                    ref var rotation = ref SystemAPI.GetComponentRW<RotationPort>(port.Entity).ValueRW;
                    rotation.Value = rotationValue;
                    break;
                case PortType.Scale:
                    port.GetValue(out float scaleValue);
                    ref var scale = ref SystemAPI.GetComponentRW<ScalePort>(port.Entity).ValueRW;
                    scale.Value = scaleValue;
                    break;
                default:
                    throw new NotImplementedException();
            }

            ref Dirty dirty = ref SystemAPI.GetComponentRW<Dirty>(port.Entity).ValueRW;
            dirty = true;
        }

        private Entity AddNode(float2 position, NodeType type) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            var entity = EntityManager.CreateEntity();

            ecb.AddComponent(entity, Node.Create(position, type));
            ecb.AddComponent<Dirty>(entity);
            ecb.AddComponent<SelectedProperties>(entity);
            ecb.SetName(entity, type.GetDisplayName());

            ecb.AddBuffer<InputPortReference>(entity);
            if (type == NodeType.Anchor) {
                var positionPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(positionPort, Port.Create(PortType.Position, true));
                ecb.AddComponent<Dirty>(positionPort, true);
                ecb.AddComponent<PositionPort>(positionPort);
                ecb.AppendToBuffer<InputPortReference>(entity, positionPort);
                ecb.SetName(positionPort, PortType.Position.GetDisplayName(true));

                var rollPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(rollPort, Port.Create(PortType.Roll, true));
                ecb.AddComponent<Dirty>(rollPort, true);
                ecb.AddComponent<RollPort>(rollPort, 0f);
                ecb.AppendToBuffer<InputPortReference>(entity, rollPort);
                ecb.SetName(rollPort, PortType.Roll.GetDisplayName(true));

                var pitchPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(pitchPort, Port.Create(PortType.Pitch, true));
                ecb.AddComponent<Dirty>(pitchPort, true);
                ecb.AddComponent<PitchPort>(pitchPort, 0f);
                ecb.AppendToBuffer<InputPortReference>(entity, pitchPort);
                ecb.SetName(pitchPort, PortType.Pitch.GetDisplayName(true));

                var yawPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(yawPort, Port.Create(PortType.Yaw, true));
                ecb.AddComponent<Dirty>(yawPort, true);
                ecb.AddComponent<YawPort>(yawPort, 0f);
                ecb.AppendToBuffer<InputPortReference>(entity, yawPort);
                ecb.SetName(yawPort, PortType.Yaw.GetDisplayName(true));

                var velocityPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(velocityPort, Port.Create(PortType.Velocity, true));
                ecb.AddComponent<Dirty>(velocityPort, true);
                ecb.AddComponent<VelocityPort>(velocityPort, 10f);
                ecb.AppendToBuffer<InputPortReference>(entity, velocityPort);
                ecb.SetName(velocityPort, PortType.Velocity.GetDisplayName(true));

                var heartPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(heartPort, Port.Create(PortType.Heart, true));
                ecb.AddComponent<Dirty>(heartPort, true);
                ecb.AddComponent<HeartPort>(heartPort, HEART_BASE);
                ecb.AppendToBuffer<InputPortReference>(entity, heartPort);
                ecb.SetName(heartPort, PortType.Heart.GetDisplayName(true));

                var frictionPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(frictionPort, Port.Create(PortType.Friction, true));
                ecb.AddComponent<Dirty>(frictionPort, true);
                ecb.AddComponent<FrictionPort>(frictionPort, FRICTION_BASE);
                ecb.AppendToBuffer<InputPortReference>(entity, frictionPort);
                ecb.SetName(frictionPort, PortType.Friction.GetDisplayName(true));

                var resistancePort = ecb.CreateEntity();
                ecb.AddComponent<Port>(resistancePort, Port.Create(PortType.Resistance, true));
                ecb.AddComponent<Dirty>(resistancePort, true);
                ecb.AddComponent<ResistancePort>(resistancePort, RESISTANCE_BASE);
                ecb.AppendToBuffer<InputPortReference>(entity, resistancePort);
                ecb.SetName(resistancePort, PortType.Resistance.GetDisplayName(true));
            }

            if (type == NodeType.ForceSection
                || type == NodeType.GeometricSection
                || type == NodeType.CurvedSection
                || type == NodeType.CopyPathSection
                || type == NodeType.Bridge
                || type == NodeType.Reverse) {
                var inputPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(inputPort, Port.Create(PortType.Anchor, true));
                ecb.AddComponent<Dirty>(inputPort, true);
                ecb.AddComponent<AnchorPort>(inputPort, PointData.Create());
                ecb.AppendToBuffer<InputPortReference>(entity, inputPort);
                ecb.SetName(inputPort, PortType.Anchor.GetDisplayName(true));
            }

            if (type == NodeType.Bridge) {
                var targetPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(targetPort, Port.Create(PortType.Anchor, true));
                ecb.AddComponent<Dirty>(targetPort, true);
                ecb.AddComponent<AnchorPort>(targetPort, PointData.Create());
                ecb.AppendToBuffer<InputPortReference>(entity, targetPort);
                ecb.SetName(targetPort, PortType.Anchor.GetDisplayName(true, 1));
            }

            if (type == NodeType.CopyPathSection
                || type == NodeType.ReversePath) {
                var pathPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(pathPort, Port.Create(PortType.Path, true));
                ecb.AddComponent<Dirty>(pathPort, true);
                ecb.AddBuffer<PathPort>(pathPort);
                ecb.AppendToBuffer<InputPortReference>(entity, pathPort);
                ecb.SetName(pathPort, PortType.Path.GetDisplayName(true));
            }

            if (type == NodeType.ForceSection || type == NodeType.GeometricSection) {
                var durationPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(durationPort, Port.Create(PortType.Duration, true));
                ecb.AddComponent<Dirty>(durationPort, true);
                ecb.AddComponent<DurationPort>(durationPort, 1f);
                ecb.AppendToBuffer<InputPortReference>(entity, durationPort);
                ecb.SetName(durationPort, PortType.Duration.GetDisplayName(true));
            }

            if (type == NodeType.CurvedSection) {
                var defaultCurveData = CurveData.Default;

                var radiusPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(radiusPort, Port.Create(PortType.Radius, true));
                ecb.AddComponent<Dirty>(radiusPort, true);
                ecb.AddComponent<RadiusPort>(radiusPort, defaultCurveData.Radius);
                ecb.AppendToBuffer<InputPortReference>(entity, radiusPort);
                ecb.SetName(radiusPort, PortType.Radius.GetDisplayName(true));

                var arcPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(arcPort, Port.Create(PortType.Arc, true));
                ecb.AddComponent<Dirty>(arcPort, true);
                ecb.AddComponent<ArcPort>(arcPort, defaultCurveData.Arc);
                ecb.AppendToBuffer<InputPortReference>(entity, arcPort);
                ecb.SetName(arcPort, PortType.Arc.GetDisplayName(true));

                var axisPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(axisPort, Port.Create(PortType.Axis, true));
                ecb.AddComponent<Dirty>(axisPort, true);
                ecb.AddComponent<AxisPort>(axisPort, defaultCurveData.Axis);
                ecb.AppendToBuffer<InputPortReference>(entity, axisPort);
                ecb.SetName(axisPort, PortType.Axis.GetDisplayName(true));

                var leadInPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(leadInPort, Port.Create(PortType.LeadIn, true));
                ecb.AddComponent<Dirty>(leadInPort, true);
                ecb.AddComponent<LeadInPort>(leadInPort, defaultCurveData.LeadIn);
                ecb.AppendToBuffer<InputPortReference>(entity, leadInPort);
                ecb.SetName(leadInPort, PortType.LeadIn.GetDisplayName(true));

                var leadOutPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(leadOutPort, Port.Create(PortType.LeadOut, true));
                ecb.AddComponent<Dirty>(leadOutPort, true);
                ecb.AddComponent<LeadOutPort>(leadOutPort, defaultCurveData.LeadOut);
                ecb.AppendToBuffer<InputPortReference>(entity, leadOutPort);
                ecb.SetName(leadOutPort, PortType.LeadOut.GetDisplayName(true));
            }

            if (type == NodeType.Mesh) {
                var positionPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(positionPort, Port.Create(PortType.Position, true));
                ecb.AddComponent<Dirty>(positionPort, true);
                ecb.AddComponent<PositionPort>(positionPort, float3.zero);
                ecb.AppendToBuffer<InputPortReference>(entity, positionPort);
                ecb.SetName(positionPort, PortType.Position.GetDisplayName(true));

                var rotationPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(rotationPort, Port.Create(PortType.Rotation, true));
                ecb.AddComponent<Dirty>(rotationPort, true);
                ecb.AddComponent<RotationPort>(rotationPort, float3.zero);
                ecb.AppendToBuffer<InputPortReference>(entity, rotationPort);
                ecb.SetName(rotationPort, PortType.Rotation.GetDisplayName(true));

                var scalePort = ecb.CreateEntity();
                ecb.AddComponent<Port>(scalePort, Port.Create(PortType.Scale, true));
                ecb.AddComponent<Dirty>(scalePort, true);
                ecb.AddComponent<ScalePort>(scalePort, 1f);
                ecb.AppendToBuffer<InputPortReference>(entity, scalePort);
                ecb.SetName(scalePort, PortType.Scale.GetDisplayName(true));
            }

            PointData anchor = PointData.Create();
            ecb.AddComponent(entity, new Anchor {
                Value = anchor,
            });

            if (type == NodeType.ForceSection
                || type == NodeType.GeometricSection
                || type == NodeType.CurvedSection
                || type == NodeType.CopyPathSection
                || type == NodeType.Bridge
                || type == NodeType.ReversePath) {
                ecb.AddBuffer<Point>(entity);
            }

            if (type == NodeType.ForceSection
                || type == NodeType.GeometricSection
                || type == NodeType.CurvedSection
                || type == NodeType.CopyPathSection
                || type == NodeType.Bridge) {
                ecb.AddComponent<Render>(entity, true);
                ecb.AddComponent(entity, PropertyOverrides.Default);
                ecb.AddComponent<SelectedProperties>(entity);
                ecb.AddBuffer<FixedVelocityKeyframe>(entity);
                ecb.AddBuffer<HeartKeyframe>(entity);
                ecb.AddBuffer<FrictionKeyframe>(entity);
                ecb.AddBuffer<ResistanceKeyframe>(entity);
            }

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection) {
                ecb.AddComponent(entity, new Duration {
                    Type = DurationType.Time,
                    Value = 1f,
                });
                ecb.AddBuffer<RollSpeedKeyframe>(entity);

                if (type == NodeType.ForceSection) {
                    ecb.AddBuffer<NormalForceKeyframe>(entity);
                    ecb.AddBuffer<LateralForceKeyframe>(entity);
                }
                else if (type == NodeType.GeometricSection) {
                    ecb.AddBuffer<PitchSpeedKeyframe>(entity);
                    ecb.AddBuffer<YawSpeedKeyframe>(entity);
                }
            }

            if (type == NodeType.CurvedSection) {
                ecb.AddComponent(entity, CurveData.Default);
                ecb.AddBuffer<RollSpeedKeyframe>(entity);
            }

            if (type == NodeType.CopyPathSection) {
                ecb.AddComponent<CopyPathSectionTag>(entity);
            }
            else if (type == NodeType.Bridge) {
                ecb.AddComponent<BridgeTag>(entity);
            }
            else if (type == NodeType.Reverse) {
                ecb.AddComponent<ReverseTag>(entity);
            }
            else if (type == NodeType.ReversePath) {
                ecb.AddComponent<ReversePathTag>(entity);
            }

            ecb.AddBuffer<OutputPortReference>(entity);
            if (type == NodeType.Anchor
                || type == NodeType.ForceSection
                || type == NodeType.GeometricSection
                || type == NodeType.CurvedSection
                || type == NodeType.CopyPathSection
                || type == NodeType.Bridge
                || type == NodeType.Reverse) {
                var outputPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(outputPort, Port.Create(PortType.Anchor, false));
                ecb.AddComponent<Dirty>(outputPort);
                ecb.AddComponent<AnchorPort>(outputPort, anchor);
                ecb.AppendToBuffer<OutputPortReference>(entity, outputPort);
                ecb.SetName(outputPort, PortType.Anchor.GetDisplayName(false));
            }

            if (type == NodeType.ForceSection
                || type == NodeType.GeometricSection
                || type == NodeType.CurvedSection
                || type == NodeType.CopyPathSection
                || type == NodeType.Bridge
                || type == NodeType.ReversePath) {
                var pathPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(pathPort, Port.Create(PortType.Path, false));
                ecb.AddComponent<Dirty>(pathPort, true);
                ecb.AddBuffer<PathPort>(pathPort);
                ecb.AppendToBuffer<OutputPortReference>(entity, pathPort);
                ecb.SetName(pathPort, PortType.Path.GetDisplayName(false));
            }

            ecb.Playback(EntityManager);

            return entity;
        }

        private void AddConnectedNode(PortData source, float2 position, NodeType nodeType, int index) {
            var node = AddNode(position, nodeType);
            Entity sourceEntity = Entity.Null;
            Entity targetEntity = Entity.Null;
            var inputs = SystemAPI.GetBuffer<InputPortReference>(node);
            sourceEntity = source.Entity;
            targetEntity = inputs[index].Value;
            AddConnection(sourceEntity, targetEntity);
        }

        private void CenterOnSelection() {
            int count = _data.Nodes.Count * 4;
            var portToPos = new NativeHashMap<Entity, Vector2>(count, Allocator.Temp);
            Vector2 center = Vector2.zero;
            int selectedCount = 0;

            foreach (var (_, nodeData) in _data.Nodes) {
                Vector2 nodeCenter = _view.GetNodeVisualCenter(nodeData.Entity);
                foreach (var port in nodeData.Inputs.Keys) portToPos[port] = nodeCenter;
                foreach (var port in nodeData.Outputs.Keys) portToPos[port] = nodeCenter;
            }

            foreach (var (_, nodeData) in _data.Nodes) {
                if (nodeData.Selected) {
                    center += _view.GetNodeVisualCenter(nodeData.Entity);
                    selectedCount++;
                }
            }

            foreach (var (_, edgeData) in _data.Edges) {
                if (!edgeData.Selected) continue;
                if (portToPos.TryGetValue(edgeData.Source, out var sourcePos) &&
                    portToPos.TryGetValue(edgeData.Target, out var targetPos)) {
                    center += (sourcePos + targetPos) * 0.5f;
                    selectedCount++;
                }
            }

            if (selectedCount > 0) {
                center /= selectedCount;
                var viewCenter = new Vector2(_view.resolvedStyle.width, _view.resolvedStyle.height) * 0.5f;
                _data.Pan = viewCenter - (center * _data.Zoom);
            }

            portToPos.Dispose();
        }

        private void CopySelectedNodes() {
            _clipboardData = null;
            _clipboardCenter = float2.zero;
            _clipboardPan = (float2)_data.Pan;

            if (!_data.HasSelectedNodes) return;

            int selectedNodeCount = 0;
            float2 center = float2.zero;
            foreach (var node in _data.Nodes.Values) {
                if (!node.Selected) continue;
                var entity = node.Entity;
                float2 position = SystemAPI.GetComponent<Node>(entity).Position;
                center += position;
                selectedNodeCount++;
            }
            center /= selectedNodeCount;
            _clipboardCenter = center;

            var clipboardGraph = new SerializedGraph {
                Nodes = new NativeArray<SerializedNode>(selectedNodeCount, Allocator.Temp)
            };
            var nodeOffsets = new NativeArray<float2>(selectedNodeCount, Allocator.Temp);

            int nodeIndex = 0;
            var portIdMap = new Dictionary<Entity, uint>();
            foreach (var node in _data.Nodes.Values) {
                if (!node.Selected) continue;
                var entity = node.Entity;
                var inputPorts = SystemAPI.GetBuffer<InputPortReference>(entity);
                var outputPorts = SystemAPI.GetBuffer<OutputPortReference>(entity);

                foreach (var port in inputPorts) {
                    portIdMap[port.Value] = SystemAPI.GetComponent<Port>(port.Value).Id;
                }
                foreach (var port in outputPorts) {
                    portIdMap[port.Value] = SystemAPI.GetComponent<Port>(port.Value).Id;
                }

                var serializedNode = Serialization.SerializationSystem.Instance.SerializeNode(entity, Allocator.Temp);
                float2 position = SystemAPI.GetComponent<Node>(entity).Position;
                nodeOffsets[nodeIndex] = position - center;
                clipboardGraph.Nodes[nodeIndex++] = serializedNode;
            }

            int edgeCount = 0;
            foreach (var edge in _data.Edges.Values) {
                var connection = SystemAPI.GetComponent<Connection>(edge.Entity);
                if (portIdMap.ContainsKey(connection.Source) && portIdMap.ContainsKey(connection.Target)) {
                    edgeCount++;
                }
            }

            clipboardGraph.Edges = new NativeArray<SerializedEdge>(edgeCount, Allocator.Temp);

            int edgeIndex = 0;
            foreach (var edge in _data.Edges.Values) {
                var connection = SystemAPI.GetComponent<Connection>(edge.Entity);
                if (portIdMap.ContainsKey(connection.Source) && portIdMap.ContainsKey(connection.Target)) {
                    clipboardGraph.Edges[edgeIndex++] = new SerializedEdge {
                        Id = connection.Id,
                        SourceId = portIdMap[connection.Source],
                        TargetId = portIdMap[connection.Target],
                        Selected = connection.Selected
                    };
                }
            }

            var clipboardData = new ClipboardData {
                Graph = clipboardGraph,
                NodeOffsets = nodeOffsets,
                Center = center
            };

            _clipboardData = ClipboardSerializer.Serialize(ref clipboardData);
            clipboardData.Dispose();
        }

        private void PasteNodes(float2 pastePosition) {
            if (_clipboardData == null) return;

            ClearSelection();

            var clipboardData = ClipboardSerializer.Deserialize(_clipboardData);
            var portMap = new Dictionary<uint, uint>();

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < clipboardData.Graph.Nodes.Length; i++) {
                var nodeData = clipboardData.Graph.Nodes[i];
                var offset = clipboardData.NodeOffsets[i];
                var newPosition = pastePosition + offset;

                var updatedNodeData = nodeData;
                updatedNodeData.Node.Position = newPosition;
                updatedNodeData.Node.Selected = true;

                for (int j = 0; j < updatedNodeData.InputPorts.Length; j++) {
                    var port = updatedNodeData.InputPorts[j];
                    uint oldId = port.Port.Id;
                    uint newId = Uuid.Create();
                    portMap[oldId] = newId;
                    port.Port.Id = newId;
                    updatedNodeData.InputPorts[j] = port;
                }

                for (int j = 0; j < updatedNodeData.OutputPorts.Length; j++) {
                    var port = updatedNodeData.OutputPorts[j];
                    uint oldId = port.Port.Id;
                    uint newId = Uuid.Create();
                    portMap[oldId] = newId;
                    port.Port.Id = newId;
                    updatedNodeData.OutputPorts[j] = port;
                }

                Serialization.SerializationSystem.Instance.DeserializeNode(updatedNodeData, ecb);
            }

            ecb.Playback(EntityManager);

            using var ports = _portQuery.ToEntityArray(Allocator.Temp);
            var lookup = new NativeHashMap<uint, Entity>(ports.Length, Allocator.Temp);
            foreach (var port in ports) {
                var id = SystemAPI.GetComponent<Port>(port).Id;
                lookup[id] = port;
            }

            using var connectionEcb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var copiedEdge in clipboardData.Graph.Edges) {
                if (portMap.TryGetValue(copiedEdge.SourceId, out var newSourceId) &&
                    portMap.TryGetValue(copiedEdge.TargetId, out var newTargetId) &&
                    lookup.TryGetValue(newSourceId, out var source) &&
                    lookup.TryGetValue(newTargetId, out var target)) {

                    var entity = connectionEcb.CreateEntity();
                    bool selected = copiedEdge.Selected;
                    var connection = Connection.Create(source, target, selected);
                    connectionEcb.AddComponent<Dirty>(entity);
                    connectionEcb.AddComponent(entity, connection);
                    connectionEcb.SetName(entity, "Connection");
                }
            }
            connectionEcb.Playback(EntityManager);

            lookup.Dispose();
            clipboardData.Dispose();
        }

        public void ResetState() {
            _data.Pan = Vector2.zero;
            _data.Zoom = 1f;
        }

        public bool CanCopy() => _data.HasSelectedNodes;
        public bool CanPaste() => _clipboardData != null;
        public bool CanDelete() => _data.HasSelectedNodes || _data.HasSelectedEdges;
        public bool CanCut() => CanCopy();
        public bool CanSelectAll() => _data.Nodes.Count > 0 || _data.Edges.Count > 0;
        public bool CanDeselectAll() => _data.HasSelectedNodes || _data.HasSelectedEdges;
        public bool CanFocus() => _data.HasSelectedNodes || _data.HasSelectedEdges;

        public void Copy() {
            CopySelectedNodes();
        }

        public void Paste(float2? worldPosition = null) {
            if (_clipboardData == null) return;

            float2 pasteCenter;
            if (worldPosition.HasValue) {
                pasteCenter = (worldPosition.Value - (float2)_data.Pan) / _data.Zoom;
            }
            else {
                float2 panDelta = (float2)_data.Pan - _clipboardPan;
                float2 offset = new(50f, 50f);
                pasteCenter = _clipboardCenter - (panDelta / _data.Zoom) + offset;
            }
            PasteNodes(pasteCenter);
        }

        public void Delete() => RemoveSelected();

        public void Cut() {
            if (CanCut()) {
                Copy();
                Delete();
            }
        }

        public void DeselectAll() => ClearSelection();

        public void Focus() {
            if (CanFocus()) {
                CenterOnSelection();
            }
        }

        private bool IsWithinNodeGraph(VisualElement element) {
            if (element == null) return false;

            var current = element;
            while (current != null) {
                if (current == _view) {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        public bool IsInBounds(Vector2 mousePosition) {
            return _view.worldBound.Contains(mousePosition);
        }
    }
}
