using System;
using System.Collections.Generic;
using KexEdit.Graph;
using KexEdit.Graph.Typed;
using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using SFB;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.Legacy.Constants;
using static KexEdit.Sim.Sim;
using static KexEdit.UI.Extensions;
using AnchorNodeBuilder = KexEdit.Sim.Nodes.Anchor.AnchorNode;
using AnchorPorts = KexEdit.Sim.Nodes.Anchor.AnchorPorts;
using CoreNodeType = KexEdit.Sim.Schema.NodeType;
using CorePoint = KexEdit.Sim.Point;
using DocumentAggregate = KexEdit.Document.Document;
using LegacyCoaster = KexEdit.Legacy.Coaster;
using NodeMeta = KexEdit.Document.NodeMeta;
using PortDataType = KexEdit.Sim.Schema.PortDataType;
using PortSpec = KexEdit.Sim.Schema.PortSpec;

namespace KexEdit.UI.NodeGraph {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class NodeGraphControlSystem : SystemBase, IEditableHandler {
        private byte[] _clipboardData = null;
        private float2 _clipboardCenter = float2.zero;
        private float2 _clipboardPan = float2.zero;
        private NodeGraphData _data;
        private NodeGraphView _view;

        private EntityQuery _coasterQuery;
        private EntityQuery _nodeQuery;
        private EntityQuery _connectionQuery;
        private EntityQuery _portQuery;

        private ref DocumentAggregate GetCoasterRef() {
            return ref SystemAPI.GetComponentRW<CoasterData>(_data.Coaster).ValueRW.Value;
        }

        private static PortDataType ToDataType(PortType portType) => portType switch {
            PortType.Anchor => PortDataType.Anchor,
            PortType.Path => PortDataType.Path,
            PortType.Position => PortDataType.Vector,
            PortType.Rotation => PortDataType.Vector,
            _ => PortDataType.Scalar
        };

        private static void AddPortToCoaster(
            ref DocumentAggregate coaster, uint nodeId, uint portId, uint encodedPortSpec, bool isInput
        ) {
            int portIndex = coaster.Graph.PortIds.Length;
            coaster.Graph.PortIds.Add(portId);
            coaster.Graph.PortTypes.Add(encodedPortSpec);
            coaster.Graph.PortOwners.Add(nodeId);
            coaster.Graph.PortIsInput.Add(isInput);
            coaster.Graph.PortIndexMap[portId] = portIndex;

            if (coaster.Graph.TryGetNodeIndex(nodeId, out int nodeIndex)) {
                if (isInput) coaster.Graph.NodeInputCount[nodeIndex]++;
                else coaster.Graph.NodeOutputCount[nodeIndex]++;
            }
        }

        private void AddNodeToCoaster(Entity entity, float2 position, NodeType type) {
            uint coreNodeType = type switch {
                NodeType.ForceSection => (uint)CoreNodeType.Force,
                NodeType.GeometricSection => (uint)CoreNodeType.Geometric,
                NodeType.CurvedSection => (uint)CoreNodeType.Curved,
                NodeType.CopyPathSection => (uint)CoreNodeType.CopyPath,
                NodeType.Anchor => (uint)CoreNodeType.Anchor,
                NodeType.Bridge => (uint)CoreNodeType.Bridge,
                NodeType.Reverse => (uint)CoreNodeType.Reverse,
                NodeType.ReversePath => (uint)CoreNodeType.ReversePath,
                _ => 0
            };

            if (coreNodeType == 0) return;

            uint nodeId = SystemAPI.GetComponent<Node>(entity).Id;
            ref var coaster = ref GetCoasterRef();

            int nodeIndex = coaster.Graph.NodeIds.Length;
            coaster.Graph.NodeIds.Add(nodeId);
            coaster.Graph.NodeTypes.Add(coreNodeType);
            coaster.Graph.NodePositions.Add(position);
            coaster.Graph.NodeInputCount.Add(0);
            coaster.Graph.NodeOutputCount.Add(0);
            coaster.Graph.NodeIndexMap[nodeId] = nodeIndex;

            var inputPorts = SystemAPI.GetBuffer<InputPortReference>(entity);
            byte inputScalarIdx = 0, inputVectorIdx = 0, inputAnchorIdx = 0, inputPathIdx = 0;
            for (int i = 0; i < inputPorts.Length; i++) {
                var portEntity = inputPorts[i].Value;
                var port = SystemAPI.GetComponent<Port>(portEntity);
                var dataType = ToDataType(port.Type);
                byte localIdx = dataType switch {
                    PortDataType.Scalar => inputScalarIdx++,
                    PortDataType.Vector => inputVectorIdx++,
                    PortDataType.Anchor => inputAnchorIdx++,
                    PortDataType.Path => inputPathIdx++,
                    _ => 0
                };
                uint encoded = new PortSpec(dataType, localIdx).ToEncoded();
                AddPortToCoaster(ref coaster, nodeId, port.Id, encoded, true);
            }

            var outputPorts = SystemAPI.GetBuffer<OutputPortReference>(entity);
            byte outputScalarIdx = 0, outputVectorIdx = 0, outputAnchorIdx = 0, outputPathIdx = 0;
            for (int i = 0; i < outputPorts.Length; i++) {
                var portEntity = outputPorts[i].Value;
                var port = SystemAPI.GetComponent<Port>(portEntity);
                var dataType = ToDataType(port.Type);
                byte localIdx = dataType switch {
                    PortDataType.Scalar => outputScalarIdx++,
                    PortDataType.Vector => outputVectorIdx++,
                    PortDataType.Anchor => outputAnchorIdx++,
                    PortDataType.Path => outputPathIdx++,
                    _ => 0
                };
                uint encoded = new PortSpec(dataType, localIdx).ToEncoded();
                AddPortToCoaster(ref coaster, nodeId, port.Id, encoded, false);
            }

            if (type == NodeType.ForceSection || type == NodeType.GeometricSection) {
                coaster.Scalars[DocumentAggregate.InputKey(nodeId, NodeMeta.Duration)] = 1f;
            }
            if (type == NodeType.GeometricSection) {
                coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.Steering)] = 1;
            }

            InitializePortScalars(ref coaster, entity, type);
        }

        private static CorePoint ConvertPointDataToPoint(in PointData pointData) {
            return new CorePoint(
                heartPosition: pointData.HeartPosition,
                direction: pointData.Direction,
                normal: pointData.Normal,
                lateral: pointData.Lateral,
                velocity: pointData.Velocity,
                normalForce: pointData.NormalForce,
                lateralForce: pointData.LateralForce,
                heartArc: pointData.HeartArc,
                spineArc: pointData.SpineArc,
                heartAdvance: pointData.HeartAdvance,
                frictionOrigin: pointData.FrictionOrigin,
                rollSpeed: pointData.RollSpeed,
                heartOffset: pointData.HeartOffset,
                friction: pointData.Friction,
                resistance: pointData.Resistance
            );
        }

        private void RebuildAnchorInCoaster(ref DocumentAggregate coaster, Entity nodeEntity) {
            uint nodeId = SystemAPI.GetComponent<Node>(nodeEntity).Id;

            ulong posKey = DocumentAggregate.InputKey(nodeId, AnchorPorts.Position);
            float3 position = coaster.Vectors.TryGetValue(posKey, out var pos) ? pos : float3.zero;

            float roll = coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Roll), out var rollVal) ? rollVal : 0f;
            float pitch = coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Pitch), out var pitchVal) ? pitchVal : 0f;
            float yaw = coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Yaw), out var yawVal) ? yawVal : 0f;

            float velocity = coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Velocity), out var v) ? v : 10f;
            float heart = coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Heart), out var h) ? h : 1.1f;
            float friction = coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Friction), out var f) ? f : 0.021f;
            float resistance = coaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, AnchorPorts.Resistance), out var r) ? r : 2e-5f;

            // Build anchor for ECS component (used by UI/visualization)
            AnchorNodeBuilder.Build(
                in position,
                pitch, yaw, roll,  // Already in radians
                velocity,
                heart, friction, resistance,
                out CorePoint anchor
            );

            ref var ecsAnchor = ref SystemAPI.GetComponentRW<Anchor>(nodeEntity).ValueRW;
            ecsAnchor.Value = new PointData {
                HeartPosition = anchor.HeartPosition,
                Direction = anchor.Direction,
                Normal = anchor.Normal,
                Lateral = anchor.Lateral,
                Roll = math.degrees(roll),
                Velocity = anchor.Velocity,
                Energy = 0f,
                NormalForce = anchor.NormalForce,
                LateralForce = anchor.LateralForce,
                HeartArc = anchor.HeartArc,
                SpineArc = anchor.SpineArc,
                HeartAdvance = anchor.HeartAdvance,
                FrictionOrigin = anchor.FrictionOrigin,
                RollSpeed = anchor.RollSpeed,
                HeartOffset = anchor.HeartOffset,
                Friction = anchor.Friction,
                Resistance = anchor.Resistance,
                Facing = 1,
            };
        }

        private void InitializePortScalars(ref DocumentAggregate coaster, Entity entity, NodeType type) {
            var inputPorts = SystemAPI.GetBuffer<InputPortReference>(entity);
            var defaultCurveData = CurveData.Default;
            uint nodeId = SystemAPI.GetComponent<Node>(entity).Id;

            for (int i = 0; i < inputPorts.Length; i++) {
                var portEntity = inputPorts[i].Value;
                var port = SystemAPI.GetComponent<Port>(portEntity);
                ulong key = DocumentAggregate.InputKey(nodeId, i);

                switch (port.Type) {
                    case PortType.Duration:
                        coaster.Scalars[key] = 1f;
                        break;
                    case PortType.Radius:
                        coaster.Scalars[key] = defaultCurveData.Radius;
                        break;
                    case PortType.Arc:
                        coaster.Scalars[key] = defaultCurveData.Arc;
                        break;
                    case PortType.Axis:
                        coaster.Scalars[key] = defaultCurveData.Axis;
                        break;
                    case PortType.LeadIn:
                        coaster.Scalars[key] = defaultCurveData.LeadIn;
                        break;
                    case PortType.LeadOut:
                        coaster.Scalars[key] = defaultCurveData.LeadOut;
                        break;
                    case PortType.InWeight:
                        coaster.Scalars[key] = 0.3f;
                        break;
                    case PortType.OutWeight:
                        coaster.Scalars[key] = 0.3f;
                        break;
                    case PortType.Start:
                        coaster.Scalars[key] = 0f;
                        break;
                    case PortType.End:
                        coaster.Scalars[key] = -1f;
                        break;
                    case PortType.Roll:
                        coaster.Scalars[key] = 0f;
                        break;
                    case PortType.Pitch:
                        coaster.Scalars[key] = 0f;
                        break;
                    case PortType.Yaw:
                        coaster.Scalars[key] = 0f;
                        break;
                    case PortType.Velocity:
                        coaster.Scalars[key] = 10f;
                        break;
                    case PortType.Heart:
                        coaster.Scalars[key] = HEART_BASE;
                        break;
                    case PortType.Friction:
                        coaster.Scalars[key] = MAX_FORCE;
                        break;
                    case PortType.Resistance:
                        coaster.Scalars[key] = RESISTANCE_BASE;
                        break;
                    case PortType.Position:
                        coaster.Vectors[key] = float3.zero;
                        break;
                }
            }
        }

        protected override void OnCreate() {
            _coasterQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LegacyCoaster, EditorCoasterTag>()
                .Build(EntityManager);
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Node, CoasterReference>()
                .Build(EntityManager);
            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Connection, CoasterReference>()
                .Build(EntityManager);
            _portQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Port>()
                .Build(EntityManager);

            RequireForUpdate<NodeGraphState>();
            RequireForUpdate<NodeGraphData>();
        }

        protected override void OnStartRunning() {
            _data = SystemAPI.ManagedAPI.GetSingleton<NodeGraphData>();

            var root = UIService.Instance.UIDocument.rootVisualElement;
            _view = root.Q<NodeGraphView>();

            var nodeGraphState = SystemAPI.GetSingleton<NodeGraphState>();
            _data.Pan = nodeGraphState.Pan;
            _data.Zoom = nodeGraphState.Zoom;

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
            _view.RegisterCallback<SteeringToggleChangeEvent>(OnSteeringToggleChange);
            _view.RegisterCallback<PriorityChangeEvent>(OnPriorityChange);
            _view.RegisterCallback<NodeGraphPanChangeEvent>(OnNodeGraphPanChange);
            _view.RegisterCallback<NodeGraphZoomChangeEvent>(OnNodeGraphZoomChange);
            _view.RegisterCallback<FocusInEvent>(OnFocusIn);
            _view.RegisterCallback<FocusOutEvent>(OnFocusOut);

            EditOperations.RegisterHandler(this);
        }

        protected override void OnDestroy() {
            EditOperations.UnregisterHandler(this);
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            _data.Coaster = _coasterQuery.IsEmpty ?
                Entity.Null :
                _coasterQuery.GetSingletonEntity();

            SyncUIState();
            InitializeNodes();
            InitializeEdges();
            UpdateNodes();
            UpdateEdges();
            _view.Draw();
        }

        private void SyncUIState() {
            var nodeGraphState = SystemAPI.GetSingleton<NodeGraphState>();
            _data.Pan = nodeGraphState.Pan;
            _data.Zoom = nodeGraphState.Zoom;
        }

        private void InitializeNodes() {
            using var entities = _nodeQuery.ToEntityArray(Allocator.Temp);
            using var set = new NativeHashSet<Entity>(entities.Length, Allocator.Temp);
            foreach (var entity in entities) {
                var coaster = SystemAPI.GetComponent<CoasterReference>(entity).Value;
                if (coaster != _data.Coaster) continue;

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

                bool steering = true;
                if (SystemAPI.HasComponent<Steering>(entity)) {
                    steering = SystemAPI.GetComponent<Steering>(entity);
                }

                var node = SystemAPI.GetComponent<Node>(entity);
                var nodeData = NodeData.Create(entity, node, durationType, render, steering);

                var inputPortReferences = SystemAPI.GetBuffer<InputPortReference>(entity);
                for (int i = 0; i < inputPortReferences.Length; i++) {
                    var portEntity = inputPortReferences[i].Value;
                    var port = SystemAPI.GetComponent<Port>(portEntity);
                    UnitsType units = GetUnits(port.Type, durationType);
                    var portData = PortData.Create(portEntity, port, entity, i, units);
                    nodeData.OrderedInputs.Add(portEntity);
                    nodeData.Inputs.Add(portEntity, portData);
                }

                var outputPortReferences = SystemAPI.GetBuffer<OutputPortReference>(entity);
                for (int i = 0; i < outputPortReferences.Length; i++) {
                    var portEntity = outputPortReferences[i].Value;
                    var port = SystemAPI.GetComponent<Port>(portEntity);
                    UnitsType units = GetUnits(port.Type, durationType);
                    var portData = PortData.Create(portEntity, port, entity, i, units);
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
                PortType.Start => UnitsType.Time,
                PortType.End => UnitsType.Time,
                _ => portType.GetUnits(),
            };
        }

        private void InitializeEdges() {
            using var entities = _connectionQuery.ToEntityArray(Allocator.Temp);
            using var set = new NativeHashSet<Entity>(entities.Length, Allocator.Temp);
            foreach (var entity in entities) {
                var coasterReference = SystemAPI.GetComponent<CoasterReference>(entity);
                if (coasterReference.Value != _data.Coaster) continue;

                set.Add(entity);

                if (_data.Edges.ContainsKey(entity)) continue;

                var connection = SystemAPI.GetComponent<Connection>(entity);
                var edgeData = EdgeData.Create(entity, connection);
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
                var node = SystemAPI.GetComponent<Node>(entity);

                DurationType durationType = DurationType.Time;
                if (SystemAPI.HasComponent<Duration>(entity)) {
                    durationType = SystemAPI.GetComponent<Duration>(entity).Type;
                }

                bool render = false;
                if (SystemAPI.HasComponent<Render>(entity)) {
                    render = SystemAPI.GetComponent<Render>(entity);
                }

                bool steering = true;
                if (SystemAPI.HasComponent<Steering>(entity)) {
                    steering = SystemAPI.GetComponent<Steering>(entity);
                }

                nodeData.Update(node, durationType, render, steering);

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
                            if (SystemAPI.HasComponent<Anchor>(entity)) {
                                PointData anchorValue = SystemAPI.GetComponent<Anchor>(entity).Value;
                                portData.SetValue(anchorValue);
                            }
                            break;
                        case PortType.Path:
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
                var connection = SystemAPI.GetComponent<Connection>(edgeData.Entity);
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
                        ShowImportDialog(_view, filePath => {
                            Undo.Record();
                            var node = AddNode(evt.ContentPosition, NodeType.Mesh);
                            EntityManager.AddComponentData(node, new NodeMeshReference {
                                Value = Entity.Null,
                                FilePath = filePath,
                                Requested = false,
                            });
                        });
                    });
                    submenu.AddItem("Append", () => {
                        var kexExtensions = new ExtensionFilter[] {
                            new("KexEdit Tracks", "kex"),
                            new("All Files", "*")
                        };
                        ShowImportDialog(_view, kexExtensions, filePath => {
                            Undo.Record();
                            var node = AddNode(evt.ContentPosition, NodeType.Append);
                            EntityManager.AddComponentData(node, new AppendReference {
                                Value = Entity.Null,
                                FilePath = filePath,
                                Loaded = false
                            });
                        });
                    });
                });
                menu.AddSeparator();
                var canPaste = EditOperations.CanPaste;
                menu.AddItem(canPaste ? "Paste" : "Cannot Paste", () => {
                    if (EditOperations.CanPaste) {
                        EditOperations.HandlePaste(evt.MousePosition);
                    }
                }, "Ctrl+V".ToPlatformShortcut(), enabled: canPaste);
            });
        }

        private void OnNodeClick(NodeClickEvent evt) {
            if (!_data.Nodes.TryGetValue(evt.Node, out var nodeData)) return;

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
                bool canCut = EditOperations.CanCut;
                bool canCopy = EditOperations.CanCopy;
                bool canPaste = EditOperations.CanPaste;

                if (nodeData.Type == NodeType.Mesh) {
                    menu.AddItem("Link", () => {
                        Undo.Record();
                        LinkMesh(nodeData);
                    });
                    menu.AddSeparator();
                }
                else if (nodeData.Type == NodeType.Append) {
                    menu.AddItem("Link", () => {
                        Undo.Record();
                        LinkAppend(nodeData);
                    });
                    menu.AddSeparator();
                }

                menu.AddPlatformItem(canCut ? "Cut" : "Cannot Cut", EditOperations.HandleCut, "Ctrl+X", enabled: canCut);
                menu.AddPlatformItem(canCopy ? "Copy" : "Cannot Copy", EditOperations.HandleCopy, "Ctrl+C", enabled: canCopy);
                menu.AddPlatformItem(canPaste ? "Paste" : "Cannot Paste", EditOperations.HandlePaste, "Ctrl+V", enabled: canPaste);
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

            float2 sourcePosition = targetNode.Position + new float2(0f, -280f);
            var node = AddNode(sourcePosition, NodeType.Anchor);

            var sourcePort = SystemAPI.GetBuffer<OutputPortReference>(node)[0];
            var targetPort = evt.Port.Entity;
            AddConnection(sourcePort, targetPort);

            PointData anchor = evt.Port.Value;
            ref var nodeAnchor = ref SystemAPI.GetComponentRW<Anchor>(node).ValueRW;
            nodeAnchor.Value = anchor;

            uint anchorNodeId = SystemAPI.GetComponent<Node>(node).Id;
            ref var coaster = ref GetCoasterRef();
            coaster.Vectors[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Position)] = anchor.HeartPosition;
            coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Roll)] = math.radians(anchor.Roll);
            coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Pitch)] = math.radians(anchor.GetPitch());
            coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Yaw)] = math.radians(anchor.GetYaw());
            coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Velocity)] = anchor.Velocity;
            coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Heart)] = anchor.HeartOffset;
            coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Friction)] = anchor.Friction;
            coaster.Scalars[DocumentAggregate.InputKey(anchorNodeId, AnchorPorts.Resistance)] = anchor.Resistance;
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

            uint nodeId = SystemAPI.GetComponent<Node>(evt.Node).Id;
            ref var coaster = ref GetCoasterRef();
            ulong durTypeKey = DocumentAggregate.InputKey(nodeId, NodeMeta.DurationType);
            coaster.Flags[durTypeKey] = evt.DurationType == KexEdit.Legacy.DurationType.Distance ? 1 : 0;

            SystemAPI.SetComponentEnabled<Dirty>(evt.Node, true);
        }

        private void OnRenderToggleChange(RenderToggleChangeEvent evt) {
            ref var render = ref SystemAPI.GetComponentRW<Render>(evt.Node).ValueRW;
            render.Value = evt.Render;

            uint nodeId = SystemAPI.GetComponent<Node>(evt.Node).Id;
            ref var coaster = ref GetCoasterRef();
            ulong renderKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Render);
            coaster.Flags[renderKey] = evt.Render ? 0 : 1;

            SystemAPI.SetComponentEnabled<Dirty>(evt.Node, true);
        }

        private void OnSteeringToggleChange(SteeringToggleChangeEvent evt) {
            ref var steering = ref SystemAPI.GetComponentRW<Steering>(evt.Node).ValueRW;
            steering.Value = evt.Steering;

            uint nodeId = SystemAPI.GetComponent<Node>(evt.Node).Id;
            ref var coaster = ref GetCoasterRef();
            ulong steeringKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Steering);
            coaster.Flags[steeringKey] = evt.Steering ? 1 : 0;

            SystemAPI.SetComponentEnabled<Dirty>(evt.Node, true);
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
                if (!SystemAPI.HasComponent<Node>(nodeData.Entity)) continue;
                ref Node node = ref SystemAPI.GetComponentRW<Node>(nodeData.Entity).ValueRW;
                node.Selected = false;
            }
            foreach (var edgeData in _data.Edges.Values) {
                if (!SystemAPI.HasComponent<Connection>(edgeData.Entity)) continue;
                ref Connection connection = ref SystemAPI.GetComponentRW<Connection>(edgeData.Entity).ValueRW;
                connection.Selected = false;
            }

            _data.HasSelectedNodes = false;
            _data.HasSelectedEdges = false;
        }

        private void AddConnection(Entity source, Entity target) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            ref var coaster = ref GetCoasterRef();

            uint sourcePortId = SystemAPI.GetComponent<Port>(source).Id;
            uint targetPortId = SystemAPI.GetComponent<Port>(target).Id;

            using var connections = _connectionQuery.ToEntityArray(Allocator.Temp);
            using var toRemove = new NativeHashSet<Entity>(connections.Length, Allocator.Temp);
            foreach (var entity in connections) {
                var existing = SystemAPI.GetComponent<Connection>(entity);
                if (existing.Target == target) {
                    for (int i = 0; i < coaster.Graph.EdgeIds.Length; i++) {
                        if (coaster.Graph.EdgeTargets[i] == targetPortId) {
                            coaster.Graph.RemoveEdge(coaster.Graph.EdgeIds[i]);
                            break;
                        }
                    }
                    toRemove.Add(entity);
                }
            }
            foreach (var entity in toRemove) {
                ecb.DestroyEntity(entity);
            }

            coaster.Graph.AddEdge(sourcePortId, targetPortId);

            var connection = ecb.CreateEntity();
            ecb.AddComponent<Dirty>(connection);
            ecb.AddComponent<CoasterReference>(connection, _data.Coaster);
            ecb.AddComponent(connection, Connection.Create(source, target, false));
            ecb.SetName(connection, "Connection");

            ecb.SetComponentEnabled<Dirty>(source, true);

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
            ref var coaster = ref GetCoasterRef();

            foreach (var node in _data.Nodes.Values) {
                if (!node.Selected) continue;
                var entity = node.Entity;
                uint nodeId = SystemAPI.GetComponent<Node>(entity).Id;

                var inputPortBuffer = SystemAPI.GetBuffer<InputPortReference>(entity);
                var outputPortBuffer = SystemAPI.GetBuffer<OutputPortReference>(entity);

                for (int i = 0; i < inputPortBuffer.Length; i++) {
                    ulong key = DocumentAggregate.InputKey(nodeId, i);
                    coaster.Scalars.Remove(key);
                    coaster.Vectors.Remove(key);
                    ecb.DestroyEntity(inputPortBuffer[i]);
                }
                foreach (var portRef in outputPortBuffer) {
                    ecb.DestroyEntity(portRef);
                }

                coaster.Graph.RemoveNodeCascade(nodeId);
                coaster.Scalars.Remove(DocumentAggregate.InputKey(nodeId, NodeMeta.Duration));
                coaster.Flags.Remove(DocumentAggregate.InputKey(nodeId, NodeMeta.DurationType));
                coaster.Flags.Remove(DocumentAggregate.InputKey(nodeId, NodeMeta.Steering));
                coaster.Flags.Remove(DocumentAggregate.InputKey(nodeId, NodeMeta.Driven));

                ecb.DestroyEntity(entity);
            }

            foreach (var edge in _data.Edges.Values) {
                if (!edge.Selected) continue;
                var entity = edge.Entity;
                var connection = SystemAPI.GetComponent<Connection>(entity);

                uint targetPortId = SystemAPI.GetComponent<Port>(connection.Target).Id;
                for (int i = 0; i < coaster.Graph.EdgeIds.Length; i++) {
                    if (coaster.Graph.EdgeTargets[i] == targetPortId) {
                        coaster.Graph.RemoveEdge(coaster.Graph.EdgeIds[i]);
                        break;
                    }
                }

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
            ShowImportDialog(_view, filePath => {
                Undo.Record();
                ref var meshReference = ref SystemAPI.GetComponentRW<NodeMeshReference>(nodeData.Entity).ValueRW;
                meshReference.Value = Entity.Null;
                meshReference.FilePath = new FixedString512Bytes(filePath);
                meshReference.Requested = false;
            });
        }

        private void LinkAppend(NodeData nodeData) {
            var appendReference = SystemAPI.GetComponent<AppendReference>(nodeData.Entity);
            var kexExtensions = new ExtensionFilter[] {
                new("KexEdit Tracks", "kex"),
                new("All Files", "*")
            };
            ShowImportDialog(_view, kexExtensions, filePath => {
                Undo.Record();
                appendReference.FilePath = filePath;
                appendReference.Value = Entity.Null;
                appendReference.Loaded = false;
            });
        }

        private void UpdateInputPortValue(PortData port) {
            ref var coaster = ref GetCoasterRef();
            uint nodeId = SystemAPI.GetComponent<Node>(port.Node).Id;
            ulong key = DocumentAggregate.InputKey(nodeId, port.Index);

            switch (port.Port.Type) {
                case PortType.Anchor:
                case PortType.Path:
                    break;
                case PortType.Duration:
                    ulong durKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Duration);
                    if (coaster.Scalars.TryGetValue(durKey, out var durationVal)) {
                        port.SetValue(durationVal);
                    }
                    break;
                case PortType.Position:
                    if (coaster.Vectors.TryGetValue(key, out var positionValue)) {
                        port.SetValue(positionValue);
                    }
                    break;
                case PortType.Roll:
                    if (coaster.Scalars.TryGetValue(key, out var rollValue)) {
                        port.SetValue(rollValue);
                    }
                    break;
                case PortType.Pitch:
                    if (coaster.Scalars.TryGetValue(key, out var pitchValue)) {
                        port.SetValue(pitchValue);
                    }
                    break;
                case PortType.Yaw:
                    if (coaster.Scalars.TryGetValue(key, out var yawValue)) {
                        port.SetValue(yawValue);
                    }
                    break;
                case PortType.Velocity:
                    if (coaster.Scalars.TryGetValue(key, out var velocityValue)) {
                        port.SetValue(velocityValue);
                    }
                    break;
                case PortType.Heart:
                    if (coaster.Scalars.TryGetValue(key, out var heartValue)) {
                        port.SetValue(heartValue);
                    }
                    break;
                case PortType.Friction:
                    if (coaster.Scalars.TryGetValue(key, out var frictionPhysicsValue)) {
                        float frictionUIValue = frictionPhysicsValue * FRICTION_PHYSICS_TO_UI_SCALE;
                        port.SetValue(frictionUIValue);
                    }
                    break;
                case PortType.Resistance:
                    if (coaster.Scalars.TryGetValue(key, out var resistancePhysicsValue)) {
                        float resistanceUIValue = resistancePhysicsValue * RESISTANCE_PHYSICS_TO_UI_SCALE;
                        port.SetValue(resistanceUIValue);
                    }
                    break;
                case PortType.Radius:
                    if (coaster.Scalars.TryGetValue(key, out var radiusValue)) {
                        port.SetValue(radiusValue);
                    }
                    break;
                case PortType.Arc:
                    if (coaster.Scalars.TryGetValue(key, out var arcValue)) {
                        port.SetValue(arcValue);
                    }
                    break;
                case PortType.Axis:
                    if (coaster.Scalars.TryGetValue(key, out var axisValue)) {
                        port.SetValue(axisValue);
                    }
                    break;
                case PortType.InWeight:
                    if (coaster.Scalars.TryGetValue(key, out var inWeightValue)) {
                        port.SetValue(inWeightValue);
                    }
                    break;
                case PortType.OutWeight:
                    if (coaster.Scalars.TryGetValue(key, out var outWeightValue)) {
                        port.SetValue(outWeightValue);
                    }
                    break;
                case PortType.LeadIn:
                    if (coaster.Scalars.TryGetValue(key, out var leadInValue)) {
                        port.SetValue(leadInValue);
                    }
                    break;
                case PortType.LeadOut:
                    if (coaster.Scalars.TryGetValue(key, out var leadOutValue)) {
                        port.SetValue(leadOutValue);
                    }
                    break;
                case PortType.Rotation:
                    break;
                case PortType.Scale:
                    if (coaster.Scalars.TryGetValue(key, out var scaleValue)) {
                        port.SetValue(scaleValue);
                    }
                    break;
                case PortType.Start:
                    if (coaster.Scalars.TryGetValue(key, out var startValue)) {
                        port.SetValue(startValue);
                    }
                    break;
                case PortType.End:
                    if (coaster.Scalars.TryGetValue(key, out var endValue)) {
                        port.SetValue(endValue);
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void ApplyInputPortValue(PortData port) {
            ref var coaster = ref GetCoasterRef();
            var node = SystemAPI.GetComponent<Node>(port.Node);
            uint nodeId = node.Id;
            var nodeType = node.Type;
            ulong key = DocumentAggregate.InputKey(nodeId, port.Index);

            switch (port.Port.Type) {
                case PortType.Anchor:
                case PortType.Path:
                    break;
                case PortType.Duration:
                    port.GetValue(out float durationValue);
                    coaster.Scalars[DocumentAggregate.InputKey(nodeId, NodeMeta.Duration)] = durationValue;
                    break;
                case PortType.Position:
                    port.GetValue(out float3 positionValue);
                    coaster.Vectors[key] = positionValue;
                    if (nodeType == NodeType.Anchor) {
                        RebuildAnchorInCoaster(ref coaster, port.Node);
                    }
                    break;
                case PortType.Roll:
                    port.GetValue(out float rollValue);
                    coaster.Scalars[key] = rollValue;
                    if (nodeType == NodeType.Anchor) {
                        RebuildAnchorInCoaster(ref coaster, port.Node);
                    }
                    break;
                case PortType.Pitch:
                    port.GetValue(out float pitchValue);
                    coaster.Scalars[key] = pitchValue;
                    if (nodeType == NodeType.Anchor) {
                        RebuildAnchorInCoaster(ref coaster, port.Node);
                    }
                    break;
                case PortType.Yaw:
                    port.GetValue(out float yawValue);
                    coaster.Scalars[key] = yawValue;
                    if (nodeType == NodeType.Anchor) {
                        RebuildAnchorInCoaster(ref coaster, port.Node);
                    }
                    break;
                case PortType.Velocity:
                    port.GetValue(out float velocityValue);
                    coaster.Scalars[key] = velocityValue;
                    if (nodeType == NodeType.Anchor) {
                        RebuildAnchorInCoaster(ref coaster, port.Node);
                    }
                    break;
                case PortType.Heart:
                    port.GetValue(out float heartValue);
                    coaster.Scalars[key] = heartValue;
                    if (nodeType == NodeType.Anchor) {
                        RebuildAnchorInCoaster(ref coaster, port.Node);
                    }
                    break;
                case PortType.Friction:
                    port.GetValue(out float frictionUIValue);
                    float frictionPhysicsValue = frictionUIValue * FRICTION_UI_TO_PHYSICS_SCALE;
                    coaster.Scalars[key] = frictionPhysicsValue;
                    if (nodeType == NodeType.Anchor) {
                        RebuildAnchorInCoaster(ref coaster, port.Node);
                    }
                    break;
                case PortType.Resistance:
                    port.GetValue(out float resistanceUIValue);
                    float resistancePhysicsValue = resistanceUIValue * RESISTANCE_UI_TO_PHYSICS_SCALE;
                    coaster.Scalars[key] = resistancePhysicsValue;
                    if (nodeType == NodeType.Anchor) {
                        RebuildAnchorInCoaster(ref coaster, port.Node);
                    }
                    break;
                case PortType.Radius:
                    port.GetValue(out float radiusValue);
                    coaster.Scalars[key] = radiusValue;
                    break;
                case PortType.Arc:
                    port.GetValue(out float arcValue);
                    coaster.Scalars[key] = arcValue;
                    break;
                case PortType.Axis:
                    port.GetValue(out float axisValue);
                    coaster.Scalars[key] = axisValue;
                    break;
                case PortType.InWeight:
                    port.GetValue(out float inWeightValue);
                    coaster.Scalars[key] = inWeightValue;
                    break;
                case PortType.OutWeight:
                    port.GetValue(out float outWeightValue);
                    coaster.Scalars[key] = outWeightValue;
                    break;
                case PortType.LeadIn:
                    port.GetValue(out float leadInValue);
                    coaster.Scalars[key] = leadInValue;
                    break;
                case PortType.LeadOut:
                    port.GetValue(out float leadOutValue);
                    coaster.Scalars[key] = leadOutValue;
                    break;
                case PortType.Rotation:
                    break;
                case PortType.Scale:
                    port.GetValue(out float scaleValue);
                    coaster.Scalars[key] = scaleValue;
                    break;
                case PortType.Start:
                    port.GetValue(out float startValue);
                    coaster.Scalars[key] = startValue;
                    break;
                case PortType.End:
                    port.GetValue(out float endValue);
                    coaster.Scalars[key] = endValue;
                    break;
                default:
                    throw new NotImplementedException();
            }

            SystemAPI.SetComponentEnabled<Dirty>(port.Entity, true);
        }

        private Entity AddNode(float2 position, NodeType type) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            var entity = EntityManager.CreateEntity();

            ecb.AddComponent(entity, Node.Create(position, type));
            ecb.AddComponent<CoasterReference>(entity, _data.Coaster);
            ecb.AddComponent<Dirty>(entity);
            ecb.AddComponent<SelectedProperties>(entity);
            ecb.SetName(entity, type.GetDisplayName());

            ecb.AddBuffer<InputPortReference>(entity);
            if (type == NodeType.Anchor) {
                var positionPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(positionPort, Port.Create(PortType.Position, true));
                ecb.AddComponent<Dirty>(positionPort);
                ecb.AppendToBuffer<InputPortReference>(entity, positionPort);
                ecb.SetName(positionPort, PortType.Position.GetDisplayName(true));

                var rollPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(rollPort, Port.Create(PortType.Roll, true));
                ecb.AddComponent<Dirty>(rollPort);
                ecb.AppendToBuffer<InputPortReference>(entity, rollPort);
                ecb.SetName(rollPort, PortType.Roll.GetDisplayName(true));

                var pitchPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(pitchPort, Port.Create(PortType.Pitch, true));
                ecb.AddComponent<Dirty>(pitchPort);
                ecb.AppendToBuffer<InputPortReference>(entity, pitchPort);
                ecb.SetName(pitchPort, PortType.Pitch.GetDisplayName(true));

                var yawPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(yawPort, Port.Create(PortType.Yaw, true));
                ecb.AddComponent<Dirty>(yawPort);
                ecb.AppendToBuffer<InputPortReference>(entity, yawPort);
                ecb.SetName(yawPort, PortType.Yaw.GetDisplayName(true));

                var velocityPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(velocityPort, Port.Create(PortType.Velocity, true));
                ecb.AddComponent<Dirty>(velocityPort);
                ecb.AppendToBuffer<InputPortReference>(entity, velocityPort);
                ecb.SetName(velocityPort, PortType.Velocity.GetDisplayName(true));

                var heartPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(heartPort, Port.Create(PortType.Heart, true));
                ecb.AddComponent<Dirty>(heartPort);
                ecb.AppendToBuffer<InputPortReference>(entity, heartPort);
                ecb.SetName(heartPort, PortType.Heart.GetDisplayName(true));

                var frictionPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(frictionPort, Port.Create(PortType.Friction, true));
                ecb.AddComponent<Dirty>(frictionPort);
                ecb.AppendToBuffer<InputPortReference>(entity, frictionPort);
                ecb.SetName(frictionPort, PortType.Friction.GetDisplayName(true));

                var resistancePort = ecb.CreateEntity();
                ecb.AddComponent<Port>(resistancePort, Port.Create(PortType.Resistance, true));
                ecb.AddComponent<Dirty>(resistancePort);
                ecb.AppendToBuffer<InputPortReference>(entity, resistancePort);
                ecb.SetName(resistancePort, PortType.Resistance.GetDisplayName(true));
            }

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection ||
                type == NodeType.CurvedSection ||
                type == NodeType.CopyPathSection ||
                type == NodeType.Bridge ||
                type == NodeType.Reverse
            ) {
                var inputPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(inputPort, Port.Create(PortType.Anchor, true));
                ecb.AddComponent<Dirty>(inputPort);
                ecb.AppendToBuffer<InputPortReference>(entity, inputPort);
                ecb.SetName(inputPort, PortType.Anchor.GetDisplayName(true));
            }

            if (type == NodeType.Bridge) {
                var targetPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(targetPort, Port.Create(PortType.Anchor, true));
                ecb.AddComponent<Dirty>(targetPort);
                ecb.AppendToBuffer<InputPortReference>(entity, targetPort);
                ecb.SetName(targetPort, PortType.Anchor.GetDisplayName(true, 1));

                var outWeightPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(outWeightPort, Port.Create(PortType.OutWeight, true));
                ecb.AddComponent<Dirty>(outWeightPort);
                ecb.AppendToBuffer<InputPortReference>(entity, outWeightPort);
                ecb.SetName(outWeightPort, PortType.OutWeight.GetDisplayName(true));

                var inWeightPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(inWeightPort, Port.Create(PortType.InWeight, true));
                ecb.AddComponent<Dirty>(inWeightPort);
                ecb.AppendToBuffer<InputPortReference>(entity, inWeightPort);
                ecb.SetName(inWeightPort, PortType.InWeight.GetDisplayName(true));
            }

            if (type == NodeType.CopyPathSection ||
                type == NodeType.ReversePath) {
                var pathPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(pathPort, Port.Create(PortType.Path, true));
                ecb.AddComponent<Dirty>(pathPort);
                ecb.AppendToBuffer<InputPortReference>(entity, pathPort);
                ecb.SetName(pathPort, PortType.Path.GetDisplayName(true));
            }

            if (type == NodeType.CopyPathSection) {
                var startPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(startPort, Port.Create(PortType.Start, true));
                ecb.AddComponent<Dirty>(startPort);
                ecb.AppendToBuffer<InputPortReference>(entity, startPort);
                ecb.SetName(startPort, PortType.Start.GetDisplayName(true));

                var endPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(endPort, Port.Create(PortType.End, true));
                ecb.AddComponent<Dirty>(endPort);
                ecb.AppendToBuffer<InputPortReference>(entity, endPort);
                ecb.SetName(endPort, PortType.End.GetDisplayName(true));
            }

            if (type == NodeType.ForceSection || type == NodeType.GeometricSection) {
                var durationPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(durationPort, Port.Create(PortType.Duration, true));
                ecb.AddComponent<Dirty>(durationPort);
                ecb.AppendToBuffer<InputPortReference>(entity, durationPort);
                ecb.SetName(durationPort, PortType.Duration.GetDisplayName(true));
            }

            if (type == NodeType.CurvedSection) {
                var radiusPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(radiusPort, Port.Create(PortType.Radius, true));
                ecb.AddComponent<Dirty>(radiusPort);
                ecb.AppendToBuffer<InputPortReference>(entity, radiusPort);
                ecb.SetName(radiusPort, PortType.Radius.GetDisplayName(true));

                var arcPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(arcPort, Port.Create(PortType.Arc, true));
                ecb.AddComponent<Dirty>(arcPort);
                ecb.AppendToBuffer<InputPortReference>(entity, arcPort);
                ecb.SetName(arcPort, PortType.Arc.GetDisplayName(true));

                var axisPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(axisPort, Port.Create(PortType.Axis, true));
                ecb.AddComponent<Dirty>(axisPort);
                ecb.AppendToBuffer<InputPortReference>(entity, axisPort);
                ecb.SetName(axisPort, PortType.Axis.GetDisplayName(true));

                var leadInPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(leadInPort, Port.Create(PortType.LeadIn, true));
                ecb.AddComponent<Dirty>(leadInPort);
                ecb.AppendToBuffer<InputPortReference>(entity, leadInPort);
                ecb.SetName(leadInPort, PortType.LeadIn.GetDisplayName(true));

                var leadOutPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(leadOutPort, Port.Create(PortType.LeadOut, true));
                ecb.AddComponent<Dirty>(leadOutPort);
                ecb.AppendToBuffer<InputPortReference>(entity, leadOutPort);
                ecb.SetName(leadOutPort, PortType.LeadOut.GetDisplayName(true));
            }

            if (type == NodeType.Mesh) {
                var positionPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(positionPort, Port.Create(PortType.Position, true));
                ecb.AddComponent<Dirty>(positionPort);
                ecb.AppendToBuffer<InputPortReference>(entity, positionPort);
                ecb.SetName(positionPort, PortType.Position.GetDisplayName(true));

                var rotationPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(rotationPort, Port.Create(PortType.Rotation, true));
                ecb.AddComponent<Dirty>(rotationPort);
                ecb.AppendToBuffer<InputPortReference>(entity, rotationPort);
                ecb.SetName(rotationPort, PortType.Rotation.GetDisplayName(true));

                var scalePort = ecb.CreateEntity();
                ecb.AddComponent<Port>(scalePort, Port.Create(PortType.Scale, true));
                ecb.AddComponent<Dirty>(scalePort);
                ecb.AppendToBuffer<InputPortReference>(entity, scalePort);
                ecb.SetName(scalePort, PortType.Scale.GetDisplayName(true));
            }


            PointData anchor = PointData.Create();
            ecb.AddComponent(entity, new Anchor {
                Value = anchor,
            });

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection ||
                type == NodeType.CurvedSection ||
                type == NodeType.CopyPathSection ||
                type == NodeType.Bridge ||
                type == NodeType.ReversePath
            ) {
                ecb.AddBuffer<CorePointBuffer>(entity);
                ecb.AddBuffer<ReadNormalForce>(entity);
                ecb.AddBuffer<ReadLateralForce>(entity);
                ecb.AddBuffer<ReadPitchSpeed>(entity);
                ecb.AddBuffer<ReadYawSpeed>(entity);
                ecb.AddBuffer<ReadRollSpeed>(entity);
            }

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection ||
                type == NodeType.CurvedSection ||
                type == NodeType.CopyPathSection ||
                type == NodeType.Bridge
            ) {
                ecb.AddComponent<Render>(entity, true);
                ecb.AddComponent(entity, PropertyOverrides.Default);
                ecb.AddComponent<SelectedProperties>(entity);
                ecb.AddComponent<TrackStyleHash>(entity);
            }

            if (type == NodeType.GeometricSection) {
                ecb.AddComponent<Steering>(entity, true);
            }

            if (type == NodeType.Mesh) {
                ecb.AddComponent<Render>(entity, true);
            }

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection) {
                ecb.AddComponent(entity, new Duration {
                    Type = DurationType.Time,
                    Value = 1f,
                });
            }

            if (type == NodeType.CurvedSection) {
                ecb.AddComponent(entity, CurveData.Default);
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
            if (type == NodeType.Anchor ||
                type == NodeType.ForceSection ||
                type == NodeType.GeometricSection ||
                type == NodeType.CurvedSection ||
                type == NodeType.CopyPathSection ||
                type == NodeType.Bridge ||
                type == NodeType.Reverse
            ) {
                var outputPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(outputPort, Port.Create(PortType.Anchor, false));
                ecb.AddComponent<Dirty>(outputPort);
                ecb.AppendToBuffer<OutputPortReference>(entity, outputPort);
                ecb.SetName(outputPort, PortType.Anchor.GetDisplayName(false));
            }

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection ||
                type == NodeType.CurvedSection ||
                type == NodeType.CopyPathSection ||
                type == NodeType.Bridge ||
                type == NodeType.ReversePath
            ) {
                var pathPort = ecb.CreateEntity();
                ecb.AddComponent<Port>(pathPort, Port.Create(PortType.Path, false));
                ecb.AddComponent<Dirty>(pathPort);
                ecb.AppendToBuffer<OutputPortReference>(entity, pathPort);
                ecb.SetName(pathPort, PortType.Path.GetDisplayName(false));
            }

            ecb.Playback(EntityManager);

            AddNodeToCoaster(entity, position, type);

            return entity;
        }

        private void AddConnectedNode(PortData source, float2 position, NodeType nodeType, int index) {
            float2 adjustedPosition = position + new float2(-56f, -56f);
            var node = AddNode(adjustedPosition, nodeType);
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
                ref var nodeGraphState = ref SystemAPI.GetSingletonRW<NodeGraphState>().ValueRW;
                nodeGraphState.Pan = viewCenter - (center * _data.Zoom);
            }

            portToPos.Dispose();
        }

        private void CopySelectedNodes() {
            _clipboardData = null;
            _clipboardCenter = float2.zero;
            _clipboardPan = (float2)_data.Pan;

            if (!_data.HasSelectedNodes) return;

            ref var coaster = ref GetCoasterRef();

            int selectedNodeCount = 0;
            float2 center = float2.zero;
            foreach (var node in _data.Nodes.Values) {
                if (!node.Selected) continue;
                float2 position = SystemAPI.GetComponent<Node>(node.Entity).Position;
                center += position;
                selectedNodeCount++;
            }
            center /= selectedNodeCount;
            _clipboardCenter = center;

            var clipboardCoaster = DocumentAggregate.Create(Allocator.Temp);
            var nodeOffsets = new NativeArray<float2>(selectedNodeCount, Allocator.Temp);
            var selectedNodeIds = new NativeHashSet<uint>(selectedNodeCount, Allocator.Temp);
            var selectedPortIds = new NativeHashSet<uint>(selectedNodeCount * 4, Allocator.Temp);

            int nodeIndex = 0;
            foreach (var node in _data.Nodes.Values) {
                if (!node.Selected) continue;
                var entity = node.Entity;
                uint nodeId = SystemAPI.GetComponent<Node>(entity).Id;
                float2 position = SystemAPI.GetComponent<Node>(entity).Position;

                selectedNodeIds.Add(nodeId);
                nodeOffsets[nodeIndex++] = position - center;

                if (!coaster.Graph.NodeIndexMap.TryGetValue(nodeId, out int srcIdx)) continue;

                int destIdx = clipboardCoaster.Graph.NodeIds.Length;
                clipboardCoaster.Graph.NodeIds.Add(nodeId);
                clipboardCoaster.Graph.NodeTypes.Add(coaster.Graph.NodeTypes[srcIdx]);
                clipboardCoaster.Graph.NodePositions.Add(coaster.Graph.NodePositions[srcIdx]);
                clipboardCoaster.Graph.NodeInputCount.Add(coaster.Graph.NodeInputCount[srcIdx]);
                clipboardCoaster.Graph.NodeOutputCount.Add(coaster.Graph.NodeOutputCount[srcIdx]);
                clipboardCoaster.Graph.NodeIndexMap[nodeId] = destIdx;
            }

            for (int i = 0; i < coaster.Graph.PortIds.Length; i++) {
                uint portOwner = coaster.Graph.PortOwners[i];
                if (!selectedNodeIds.Contains(portOwner)) continue;

                uint portId = coaster.Graph.PortIds[i];
                selectedPortIds.Add(portId);

                int destIdx = clipboardCoaster.Graph.PortIds.Length;
                clipboardCoaster.Graph.PortIds.Add(portId);
                clipboardCoaster.Graph.PortTypes.Add(coaster.Graph.PortTypes[i]);
                clipboardCoaster.Graph.PortOwners.Add(portOwner);
                clipboardCoaster.Graph.PortIsInput.Add(coaster.Graph.PortIsInput[i]);
                clipboardCoaster.Graph.PortIndexMap[portId] = destIdx;
            }

            for (int i = 0; i < coaster.Graph.EdgeIds.Length; i++) {
                uint sourcePortId = coaster.Graph.EdgeSources[i];
                uint targetPortId = coaster.Graph.EdgeTargets[i];
                if (!selectedPortIds.Contains(sourcePortId) || !selectedPortIds.Contains(targetPortId)) continue;

                uint edgeId = coaster.Graph.EdgeIds[i];
                int destIdx = clipboardCoaster.Graph.EdgeIds.Length;
                clipboardCoaster.Graph.EdgeIds.Add(edgeId);
                clipboardCoaster.Graph.EdgeSources.Add(sourcePortId);
                clipboardCoaster.Graph.EdgeTargets.Add(targetPortId);
                clipboardCoaster.Graph.EdgeIndexMap[edgeId] = destIdx;
            }

            foreach (var kv in coaster.Scalars) {
                DocumentAggregate.UnpackInputKey(kv.Key, out uint nodeId, out _);
                if (selectedNodeIds.Contains(nodeId)) {
                    clipboardCoaster.Scalars[kv.Key] = kv.Value;
                }
            }

            foreach (var kv in coaster.Vectors) {
                DocumentAggregate.UnpackInputKey(kv.Key, out uint nodeId, out _);
                if (selectedNodeIds.Contains(nodeId)) {
                    clipboardCoaster.Vectors[kv.Key] = kv.Value;
                }
            }

            foreach (var kv in coaster.Flags) {
                DocumentAggregate.UnpackInputKey(kv.Key, out uint nodeId, out _);
                if (selectedNodeIds.Contains(nodeId)) {
                    clipboardCoaster.Flags[kv.Key] = kv.Value;
                }
            }

            foreach (var kv in coaster.Keyframes.Ranges) {
                DocumentAggregate.UnpackInputKey(kv.Key, out uint nodeId, out _);
                if (selectedNodeIds.Contains(nodeId)) {
                    int2 range = kv.Value;
                    int newStart = clipboardCoaster.Keyframes.Keyframes.Length;
                    for (int i = range.x; i < range.x + range.y; i++) {
                        clipboardCoaster.Keyframes.Keyframes.Add(coaster.Keyframes.Keyframes[i]);
                    }
                    clipboardCoaster.Keyframes.Ranges[kv.Key] = new int2(newStart, range.y);
                }
            }

            selectedNodeIds.Dispose();
            selectedPortIds.Dispose();

            var clipboardData = new ClipboardData {
                Coaster = clipboardCoaster,
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
            var emptyUiState = KexEdit.Persistence.UIStateChunk.Create(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < clipboardData.Coaster.Graph.NodeIds.Length; i++) {
                var offset = clipboardData.NodeOffsets[i];
                var newPosition = pastePosition + offset;

                EcsAdapter.BuildSerializedNode(
                    in clipboardData.Coaster,
                    in emptyUiState,
                    i,
                    Allocator.Temp,
                    out var nodeData
                );

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

                SerializationSystem.Instance.DeserializeNode(updatedNodeData, _data.Coaster, ref ecb, false);
                nodeData.Dispose();
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            emptyUiState.Dispose();

            using var ports = _portQuery.ToEntityArray(Allocator.Temp);
            var lookup = new NativeHashMap<uint, Entity>(ports.Length, Allocator.Temp);
            foreach (var port in ports) {
                var id = SystemAPI.GetComponent<Port>(port).Id;
                lookup[id] = port;
            }

            using var connectionEcb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < clipboardData.Coaster.Graph.EdgeIds.Length; i++) {
                uint sourcePortId = clipboardData.Coaster.Graph.EdgeSources[i];
                uint targetPortId = clipboardData.Coaster.Graph.EdgeTargets[i];

                if (portMap.TryGetValue(sourcePortId, out var newSourceId) &&
                    portMap.TryGetValue(targetPortId, out var newTargetId) &&
                    lookup.TryGetValue(newSourceId, out var source) &&
                    lookup.TryGetValue(newTargetId, out var target)) {

                    var entity = connectionEcb.CreateEntity();
                    var connection = Connection.Create(source, target, false);
                    connectionEcb.AddComponent<Dirty>(entity);
                    connectionEcb.AddComponent<CoasterReference>(entity, _data.Coaster);
                    connectionEcb.AddComponent(entity, connection);
                    connectionEcb.SetName(entity, "Connection");
                }
            }
            connectionEcb.Playback(EntityManager);

            lookup.Dispose();
            clipboardData.Dispose();
        }

        public void ResetState() {
            ref var nodeGraphState = ref SystemAPI.GetSingletonRW<NodeGraphState>().ValueRW;
            nodeGraphState.Pan = float2.zero;
            nodeGraphState.Zoom = 1f;
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


        private void OnNodeGraphPanChange(NodeGraphPanChangeEvent evt) {
            ref var nodeGraphState = ref SystemAPI.GetSingletonRW<NodeGraphState>().ValueRW;
            nodeGraphState.Pan = evt.Pan;
        }

        private void OnNodeGraphZoomChange(NodeGraphZoomChangeEvent evt) {
            ref var nodeGraphState = ref SystemAPI.GetSingletonRW<NodeGraphState>().ValueRW;
            nodeGraphState.Zoom = evt.Zoom;
        }

        private void OnFocusIn(FocusInEvent evt) {
            EditOperations.SetActiveHandler(this);
        }

        private void OnFocusOut(FocusOutEvent evt) {
            EditOperations.SetActiveHandler(null);
        }
    }
}
