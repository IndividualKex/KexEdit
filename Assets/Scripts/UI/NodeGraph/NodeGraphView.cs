using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

namespace KexEdit.UI.NodeGraph {
    [UxmlElement]
    public partial class NodeGraphView : VisualElement {
        private const float MIN_ZOOM = 0.1f;
        private const float MAX_ZOOM = 10f;
        private const float ZOOM_SPEED = 0.3f;

        public static float LastZoomTime;

        private VisualElement _content;
        private VisualElement _edgeLayer;
        private VisualElement _nodeLayer;
        private VisualElement _container;
        private VisualElement _horizontalGuide;
        private VisualElement _verticalGuide;
        private SelectionBox _selectionBox;
        private Label _tip;

        private Dictionary<Entity, NodeGraphNode> _nodes = new();
        private Dictionary<Entity, NodeGraphEdge> _edges = new();
        private List<Entity> _nodeCache = new();
        private List<Entity> _edgeCache = new();
        private NodeGraphNodePool _nodePool;
        private Vector2 _startMousePosition;
        private bool _panning;
        private bool _boxSelecting;

        private NodeGraphData _data;

        public VisualElement EdgeLayer => _edgeLayer;
        public VisualElement NodeLayer => _nodeLayer;
        public Vector2 Pan => _data.Pan;
        public float Zoom => _data.Zoom;

        public NodeGraphView() {
            style.position = Position.Absolute;
            style.backgroundColor = s_AltDarkBackgroundColor;
            style.left = 0;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            style.overflow = Overflow.Visible;
            focusable = true;

            _tip = new Label("Right click to add node") {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    overflow = Overflow.Hidden,
                    fontSize = 12,
                    color = new Color(0.5f, 0.5f, 0.5f),
                }
            };
            Add(_tip);

            _container = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    overflow = Overflow.Hidden,
                }
            };
            Add(_container);

            _selectionBox = new SelectionBox();
            Add(_selectionBox);

            _horizontalGuide = new VisualElement {
                style = {
                    position = Position.Absolute,
                    height = 1f,
                    backgroundColor = s_BlueOutlineTransparent,
                    display = DisplayStyle.None,
                }
            };
            _verticalGuide = new VisualElement {
                style = {
                    position = Position.Absolute,
                    width = 1f,
                    backgroundColor = s_BlueOutlineTransparent,
                    display = DisplayStyle.None,
                }
            };

            _container.Add(_horizontalGuide);
            _container.Add(_verticalGuide);

            _content = new VisualElement {
                style = {
                    position = Position.Absolute,
                    width = 0,
                    height = 0,
                    backgroundColor = Color.clear,
                },
                pickingMode = PickingMode.Ignore
            };
            _container.Add(_content);

            _edgeLayer = new VisualElement {
                style = {
                    position = Position.Absolute,
                    width = 0,
                    height = 0,
                    top = 0,
                    left = 0,
                },
                pickingMode = PickingMode.Ignore
            };
            _nodeLayer = new VisualElement {
                style = {
                    position = Position.Absolute,
                    width = 0,
                    height = 0,
                    top = 0,
                    left = 0,
                },
                pickingMode = PickingMode.Ignore
            };
            _content.Add(_edgeLayer);
            _content.Add(_nodeLayer);
        }

        public void Initialize(NodeGraphData data) {
            _data = data;
            dataSource = _data;

            _nodePool = new NodeGraphNodePool(this);

            generateVisualContent += OnDrawGrid;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(OnWheel);
        }

        public void Draw() {
            InitializeNodes();
            InitializeEdges();

            _content.style.translate = new Translate(_data.Pan.x, _data.Pan.y);
            _content.style.scale = new Scale(new Vector3(_data.Zoom, _data.Zoom, 1f));

            foreach (var edge in _edges.Values) {
                edge.Draw();
            }

            MarkDirtyRepaint();
        }

        private void InitializeNodes() {
            using var toRemove = new NativeList<Entity>(Allocator.Temp);
            foreach (var entity in _nodes.Keys) {
                if (_data.Nodes.ContainsKey(entity)) continue;
                toRemove.Add(entity);
            }
            foreach (var entity in toRemove) {
                RemoveNode(entity);
            }

            foreach (var entity in _data.Nodes.Keys) {
                if (_nodes.ContainsKey(entity)) continue;
                AddNode(entity);
            }
        }

        private void InitializeEdges() {
            using var toRemove = new NativeList<Entity>(Allocator.Temp);
            foreach (var entity in _edges.Keys) {
                if (_data.Edges.ContainsKey(entity)) continue;
                toRemove.Add(entity);
            }
            foreach (var entity in toRemove) {
                RemoveEdge(entity);
            }

            foreach (var entity in _data.Edges.Keys) {
                if (_edges.ContainsKey(entity)) continue;
                AddEdge(entity);
            }
        }

        private NodeGraphNode AddNode(Entity entity) {
            var nodeData = _data.Nodes[entity];
            var node = _nodePool.Get(nodeData.Type);

            node.Bind(nodeData);

            Vector2 position = ((Vector2)nodeData.Position - _data.Pan) / _data.Zoom;
            node.style.left = position.x;
            node.style.top = position.y;

            _nodeLayer.Add(node);
            _nodes.Add(entity, node);

            _tip.style.display = DisplayStyle.None;

            return node;
        }

        private NodeGraphEdge AddEdge(Entity entity) {
            var edgeData = _data.Edges[entity];
            var source = FindPort(edgeData.Source);
            var target = FindPort(edgeData.Target);
            var edge = new NodeGraphEdge(this, edgeData, source, target);
            _edges.Add(entity, edge);
            _edgeLayer.Add(edge);
            return edge;
        }

        private NodeGraphPort FindPort(Entity entity) {
            foreach (var node in _nodes.Values) {
                if (node.Ports.TryGetValue(entity, out var port)) return port;
            }
            throw new System.Exception($"Port with entity {entity} not found");
        }

        private void RemoveNode(Entity entity) {
            var node = _nodes[entity];
            _nodeLayer.Remove(node);
            _nodes.Remove(entity);

            _nodePool.Return(node);

            _tip.style.display = _nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RemoveEdge(Entity entity) {
            var edge = _edges[entity];
            _edgeLayer.Remove(edge);
            _edges.Remove(entity);
        }

        private void UpdateSelectionBox(Vector2 position) {
            if (!_boxSelecting) return;
            _selectionBox.Draw(position);
        }

        private void SelectBox(Rect selectionRect) {
            Vector2 contentStart = (new Vector2(selectionRect.x, selectionRect.y) - _data.Pan) / _data.Zoom;
            Vector2 contentSize = selectionRect.size / _data.Zoom;
            Rect contentSpaceRect = new(contentStart, contentSize);

            _nodeCache.Clear();
            _edgeCache.Clear();

            foreach (NodeGraphNode node in _nodes.Values) {
                Vector2 nodePos = new(node.style.left.value.value, node.style.top.value.value);
                Vector2 nodeSize = new(node.resolvedStyle.width, node.resolvedStyle.height);
                Rect nodeRect = new(nodePos, nodeSize);

                if (contentSpaceRect.Overlaps(nodeRect)) {
                    _nodeCache.Add(node.Data.Entity);
                }
            }

            foreach (NodeGraphEdge edge in _edges.Values) {
                if (edge.IntersectsRect(contentSpaceRect)) {
                    _edgeCache.Add(edge.Data.Entity);
                }
            }

            var e = this.GetPooled<SelectionEvent>();
            e.Nodes = _nodeCache;
            e.Edges = _edgeCache;
            this.SendEvent(e);
        }

        private void OnDrawGrid(MeshGenerationContext ctx) {
            if (!Preferences.NodeGridSnapping) return;

            var painter = ctx.painter2D;
            Rect rect = contentRect;

            float gridSize = NODE_GRID_SIZE * _data.Zoom;
            if (gridSize < 4f) return;

            painter.strokeColor = s_MutedGridColor;
            painter.lineWidth = 1f;

            float offsetX = (_data.Pan.x % gridSize + gridSize) % gridSize;
            float offsetY = (_data.Pan.y % gridSize + gridSize) % gridSize;

            for (float x = offsetX; x < rect.width; x += gridSize) {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, rect.height));
                painter.Stroke();
            }

            for (float y = offsetY; y < rect.height; y += gridSize) {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(rect.width, y));
                painter.Stroke();
            }
        }

        private void OnMouseDown(MouseDownEvent evt) {
            Focus();

            if (evt.button == 0 && evt.target == _container) {
                _boxSelecting = true;
                _startMousePosition = evt.localMousePosition;
                _selectionBox.Begin(_startMousePosition);

                if (!evt.shiftKey) {
                    this.Send<ClearSelectionEvent>();
                }

                this.CaptureMouse();
                evt.StopPropagation();
            }

            else if (evt.button == 1 && evt.target == _container && !evt.altKey) {
                Vector2 position = evt.localMousePosition;
                Vector2 contentPosition = (position - _data.Pan) / _data.Zoom;
                var e = this.GetPooled<ViewRightClickEvent>();
                e.MousePosition = evt.localMousePosition;
                e.ContentPosition = contentPosition;
                this.SendEvent(e);
            }

            if (evt.button == 2 || (evt.button == 1 && evt.altKey)) {
                _panning = true;
                _startMousePosition = evt.localMousePosition;
                this.CaptureMouse();
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (_boxSelecting) {
                UpdateSelectionBox(evt.localMousePosition);
                evt.StopPropagation();
                return;
            }

            if (_panning) {
                Vector2 delta = evt.localMousePosition - _startMousePosition;
                delta = Preferences.AdjustPointerDelta(delta);
                _data.Pan += delta;
                _content.style.translate = new Translate(_data.Pan.x, _data.Pan.y);
                _startMousePosition = evt.localMousePosition;

                var e = this.GetPooled<NodeGraphPanChangeEvent>();
                e.Pan = _data.Pan;
                this.Send(e);

                evt.StopPropagation();
            }
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (evt.button == 0 && _boxSelecting) {
                _boxSelecting = false;
                this.ReleaseMouse();

                Rect selectionRect = _selectionBox.Close();
                SelectBox(selectionRect);
                evt.StopPropagation();
            }

            if (_panning && (evt.button == 2 || (evt.button == 1 && evt.altKey))) {
                _panning = false;
                this.ReleaseMouse();
                evt.StopPropagation();
            }
        }

        private void OnWheel(WheelEvent evt) {
            Vector2 mousePos = evt.mousePosition;
            Vector2 contentSpaceMousePos = (mousePos - _data.Pan) / _data.Zoom;

            float zoomDelta = -Preferences.AdjustScroll(evt.delta.y) * ZOOM_SPEED;
            float multiplier = zoomDelta > 0 ? 1.1f : 1f / 1.1f;
            _data.Zoom = Mathf.Clamp(_data.Zoom * Mathf.Pow(multiplier, Mathf.Abs(zoomDelta)), MIN_ZOOM, MAX_ZOOM);

            _content.style.scale = new Scale(new Vector3(_data.Zoom, _data.Zoom, 1f));
            _data.Pan = mousePos - contentSpaceMousePos * _data.Zoom;
            _content.style.translate = new Translate(_data.Pan.x, _data.Pan.y);

            LastZoomTime = Time.realtimeSinceStartup;

            var zoomEvent = this.GetPooled<NodeGraphZoomChangeEvent>();
            zoomEvent.Zoom = _data.Zoom;
            this.Send(zoomEvent);

            var panEvent = this.GetPooled<NodeGraphPanChangeEvent>();
            panEvent.Pan = _data.Pan;
            this.Send(panEvent);

            evt.StopPropagation();
        }

        public float2 SnapNodePosition(NodeGraphNode node, float2 desiredPosition) {
            const float snapThresholdScreen = 10f;
            float snapThreshold = snapThresholdScreen / _data.Zoom;

            float2 snappedPosition = desiredPosition;
            float nodeWidth = node.resolvedStyle.width;
            float nodeHeight = node.resolvedStyle.height;

            SnapInfo snapX = default;
            SnapInfo snapY = default;

            SnapInfo neighborSnapX = default;
            SnapInfo neighborSnapY = default;

            foreach (NodeGraphNode other in _nodes.Values) {
                if (other == node) continue;

                float otherLeft = other.style.left.value.value;
                float otherTop = other.style.top.value.value;
                float otherWidth = other.resolvedStyle.width;
                float otherHeight = other.resolvedStyle.height;

                if (!neighborSnapX.IsValid) {
                    TrySnapAxis(
                        desiredPosition.x, nodeWidth,
                        otherLeft, otherWidth,
                        snapThreshold, ref neighborSnapX, other
                    );
                }

                if (!neighborSnapY.IsValid) {
                    TrySnapAxis(
                        desiredPosition.y, nodeHeight,
                        otherTop, otherHeight,
                        snapThreshold, ref neighborSnapY, other
                    );
                }
            }

            if (Preferences.NodeGridSnapping) {
                float gridSize = NODE_GRID_SIZE;
                float2 gridSnappedPosition = new(
                    Mathf.Round(desiredPosition.x / gridSize) * gridSize - 2f,
                    Mathf.Round(desiredPosition.y / gridSize) * gridSize - 2f
                );

                float gridDistanceX = Mathf.Abs(desiredPosition.x - gridSnappedPosition.x);
                float gridDistanceY = Mathf.Abs(desiredPosition.y - gridSnappedPosition.y);

                if (gridDistanceX < snapThreshold) {
                    snapX.SetSnap(gridSnappedPosition.x, gridSnappedPosition.x, null);
                }
                else if (neighborSnapX.IsValid) {
                    snapX = neighborSnapX;
                }

                if (gridDistanceY < snapThreshold) {
                    snapY.SetSnap(gridSnappedPosition.y, gridSnappedPosition.y, null);
                }
                else if (neighborSnapY.IsValid) {
                    snapY = neighborSnapY;
                }
            }
            else {
                snapX = neighborSnapX;
                snapY = neighborSnapY;
            }

            if (snapX.IsValid) snappedPosition.x = snapX.TargetPosition;
            if (snapY.IsValid) snappedPosition.y = snapY.TargetPosition;

            UpdateGuides(neighborSnapX, neighborSnapY, snappedPosition, nodeWidth, nodeHeight);

            return snappedPosition;
        }

        private void TrySnapAxis(
            float nodePos, float nodeSize,
            float otherPos, float otherSize,
            float threshold, ref SnapInfo snap, NodeGraphNode other
        ) {
            float nodeCenter = nodePos + nodeSize * 0.5f;
            float nodeEnd = nodePos + nodeSize;
            float otherCenter = otherPos + otherSize * 0.5f;
            float otherEnd = otherPos + otherSize;

            float bestDistance = threshold;

            // Edge to edge alignment
            float distance = Mathf.Abs(nodePos - otherPos);
            if (distance < bestDistance) {
                bestDistance = distance;
                snap.SetSnap(otherPos, otherPos, other);
            }

            // Center to center alignment
            distance = Mathf.Abs(nodeCenter - otherCenter);
            if (distance < bestDistance) {
                bestDistance = distance;
                snap.SetSnap(otherCenter - nodeSize * 0.5f, otherCenter, other);
            }

            // End to end alignment
            distance = Mathf.Abs(nodeEnd - otherEnd);
            if (distance < bestDistance) {
                bestDistance = distance;
                snap.SetSnap(otherEnd - nodeSize, otherEnd, other);
            }
        }

        private void UpdateGuides(SnapInfo snapX, SnapInfo snapY, float2 position, float nodeWidth, float nodeHeight) {
            _horizontalGuide.style.display = DisplayStyle.None;
            _verticalGuide.style.display = DisplayStyle.None;

            const float guideExtension = 1000f;

            // Draw vertical guide (X axis snapping) - only for neighbor snapping
            if (snapX.IsValid && snapX.TargetNode != null) {
                float guideX = snapX.GuidePosition;
                float containerGuideX = guideX * _data.Zoom + _data.Pan.x;

                float nodeTop = position.y;
                float nodeBottom = position.y + nodeHeight;

                float otherTop = snapX.TargetNode.style.top.value.value;
                float otherBottom = otherTop + snapX.TargetNode.resolvedStyle.height;
                float minY = Mathf.Min(nodeTop, otherTop) - guideExtension;
                float maxY = Mathf.Max(nodeBottom, otherBottom) + guideExtension;

                float containerMinY = minY * _data.Zoom + _data.Pan.y;
                float containerMaxY = maxY * _data.Zoom + _data.Pan.y;

                _verticalGuide.style.left = containerGuideX;
                _verticalGuide.style.top = containerMinY;
                _verticalGuide.style.height = containerMaxY - containerMinY;
                _verticalGuide.style.display = DisplayStyle.Flex;
            }

            // Draw horizontal guide (Y axis snapping) - only for neighbor snapping
            if (snapY.IsValid && snapY.TargetNode != null) {
                float guideY = snapY.GuidePosition;
                float containerGuideY = guideY * _data.Zoom + _data.Pan.y;

                float nodeLeft = position.x;
                float nodeRight = position.x + nodeWidth;

                float otherLeft = snapY.TargetNode.style.left.value.value;
                float otherRight = otherLeft + snapY.TargetNode.resolvedStyle.width;
                float minX = Mathf.Min(nodeLeft, otherLeft) - guideExtension;
                float maxX = Mathf.Max(nodeRight, otherRight) + guideExtension;

                float containerMinX = minX * _data.Zoom + _data.Pan.x;
                float containerMaxX = maxX * _data.Zoom + _data.Pan.x;

                _horizontalGuide.style.top = containerGuideY;
                _horizontalGuide.style.left = containerMinX;
                _horizontalGuide.style.width = containerMaxX - containerMinX;
                _horizontalGuide.style.display = DisplayStyle.Flex;
            }
        }

        public void ClearGuides() {
            _horizontalGuide.style.display = DisplayStyle.None;
            _verticalGuide.style.display = DisplayStyle.None;
        }

        public Vector2 GetNodeVisualCenter(Entity entity) {
            if (_nodes.TryGetValue(entity, out var node)) {
                Vector2 topLeft = (Vector2)_data.Nodes[entity].Position;
                Vector2 size = new Vector2(node.resolvedStyle.width, node.resolvedStyle.height);
                return topLeft + size * 0.5f;
            }
            return Vector2.zero;
        }

        private struct SnapInfo {
            public bool IsValid;
            public float TargetPosition;
            public float GuidePosition;
            public NodeGraphNode TargetNode;

            public void SetSnap(float targetPos, float guidePos, NodeGraphNode targetNode) {
                IsValid = true;
                TargetPosition = targetPos;
                GuidePosition = guidePos;
                TargetNode = targetNode;
            }
        }
    }
}
