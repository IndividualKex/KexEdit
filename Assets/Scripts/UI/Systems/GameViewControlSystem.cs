using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using KexEdit.UI.NodeGraph;
using KexEdit.UI.Timeline;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class GameViewControlSystem : SystemBase, IEditableHandler {
        public static GameViewControlSystem Instance { get; private set; }

        private UnityEngine.Camera _camera;
        private NodeGraphView _nodeGraphView;
        private TimelineView _timelineView;
        private GameView _gameView;
        private NodeGraphData _nodeGraphData;

        public GameViewControlSystem() {
            Instance = this;
        }

        protected override void OnCreate() {
            RequireForUpdate<NodeGraphData>();
            RequireForUpdate<GameViewData>();
        }

        protected override void OnStartRunning() {
            _nodeGraphData = SystemAPI.ManagedAPI.GetSingleton<NodeGraphData>();

            _camera = UnityEngine.Camera.main;

            var root = UIService.Instance.UIDocument.rootVisualElement;
            _nodeGraphView = root.Q<NodeGraphView>();
            _timelineView = root.Q<TimelineView>();
            _gameView = root.Q<GameView>();

            _gameView.RegisterCallback<MouseDownEvent>(OnGameViewMouseDown);
            _gameView.RegisterCallback<FocusInEvent>(OnFocusIn);
            _gameView.RegisterCallback<FocusOutEvent>(OnFocusOut);

            EditOperations.RegisterHandler(this);
        }

        protected override void OnDestroy() {
            EditOperations.UnregisterHandler(this);
        }

        private void OnGameViewMouseDown(MouseDownEvent evt) {
            bool altOrCmdPressed = Keyboard.current.leftAltKey.isPressed ||
                                  Keyboard.current.rightAltKey.isPressed ||
                                  Keyboard.current.leftCommandKey.isPressed ||
                                  Keyboard.current.rightCommandKey.isPressed;

            if (altOrCmdPressed || OrbitCameraSystem.IsRideCameraActive || evt.button != 0) return;

            bool shiftPressed = Keyboard.current.shiftKey.isPressed;

            var gameViewData = SystemAPI.GetSingleton<GameViewData>();
            var intersectionKeyframe = gameViewData.IntersectionKeyframe;

            if (intersectionKeyframe.Node != Entity.Null &&
                SystemAPI.HasComponent<Node>(intersectionKeyframe.Node)) {
                var node = SystemAPI.GetComponent<Node>(intersectionKeyframe.Node);
                if (!node.Selected) {
                    var nodeClickEvent = _nodeGraphView.GetPooled<NodeClickEvent>();
                    nodeClickEvent.Node = intersectionKeyframe.Node;
                    nodeClickEvent.ShiftKey = false;
                    _nodeGraphView.Send(nodeClickEvent);
                    _timelineView.Send<ForceUpdateEvent>();
                }

                var keyframeClickEvent = _timelineView.GetPooled<KeyframeClickEvent>();
                keyframeClickEvent.Keyframe = intersectionKeyframe.Keyframe;
                keyframeClickEvent.ShiftKey = shiftPressed;
                _timelineView.Send(keyframeClickEvent);
                return;
            }

            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            float xNorm = evt.localMousePosition.x / _gameView.resolvedStyle.width;
            float yNorm = 1f - evt.localMousePosition.y / _gameView.resolvedStyle.height;

            var ray = _camera.ViewportPointToRay(new Vector3(xNorm, yNorm, 0f));

            RaycastInput raycast = new() {
                Start = ray.origin,
                End = ray.origin + ray.direction * 1000f,
                Filter = new CollisionFilter() {
                    BelongsTo = ~0u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                }
            };

            if (collisionWorld.CastRay(raycast, out var hit)) {
                if (SystemAPI.HasComponent<NodeReference>(hit.Entity)) {
                    Entity entity = SystemAPI.GetComponent<NodeReference>(hit.Entity);
                    var e = _nodeGraphView.GetPooled<NodeClickEvent>();
                    e.Node = entity;
                    e.ShiftKey = shiftPressed;
                    _nodeGraphView.Send(e);
                }
            }
            else if (!shiftPressed) {
                _nodeGraphView.Send<ClearSelectionEvent>();
            }
        }

        private bool TryGetSelectionBounds(out Bounds bounds) {
            bounds = default;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            bool foundAny = false;

            foreach (var node in SystemAPI.Query<NodeAspect>().WithAll<Point>()) {
                if (!node.Selected) continue;

                var pointBuffer = SystemAPI.GetBuffer<Point>(node.Self);
                if (pointBuffer.Length < 2) continue;

                int midIndex = pointBuffer.Length / 2;
                using NativeArray<int> indices = new(3, Allocator.Temp) {
                    [0] = 0,
                    [1] = midIndex,
                    [2] = pointBuffer.Length - 1
                };

                foreach (var idx in indices) {
                    PointData point = pointBuffer[idx];
                    var pos = point.Position;
                    if (pos.x < minX) minX = pos.x;
                    if (pos.x > maxX) maxX = pos.x;
                    if (pos.y < minY) minY = pos.y;
                    if (pos.y > maxY) maxY = pos.y;
                    if (pos.z < minZ) minZ = pos.z;
                    if (pos.z > maxZ) maxZ = pos.z;
                    foundAny = true;
                }
            }

            if (!foundAny) return false;

            Vector3 min = new(minX, minY, minZ);
            Vector3 max = new(maxX, maxY, maxZ);
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            bounds = new Bounds(center, size);
            return true;
        }

        private bool IsWithinGameView(VisualElement element) {
            if (element == null) return false;

            var current = element;
            while (current != null) {
                if (current == _gameView) {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        public void ResetGameViewState() {
            OrbitCameraSystem.ResetState();
        }

        public bool CanCopy() => false;
        public bool CanPaste() => false;
        public bool CanDelete() => false;
        public bool CanCut() => false;
        public bool CanSelectAll() => _nodeGraphData.Nodes.Count > 0;
        public bool CanDeselectAll() => _nodeGraphData.HasSelectedNodes;
        public bool CanFocus() => true;

        public void Copy() { }
        public void Paste(float2? worldPosition = null) { }
        public void Delete() { }
        public void Cut() { }
        public void SelectAll() {
            foreach (var nodeData in _nodeGraphData.Nodes.Values) {
                ref Node node = ref SystemAPI.GetComponentRW<Node>(nodeData.Entity).ValueRW;
                node.Selected = true;
            }
        }

        public void DeselectAll() {
            foreach (var nodeData in _nodeGraphData.Nodes.Values) {
                ref Node node = ref SystemAPI.GetComponentRW<Node>(nodeData.Entity).ValueRW;
                node.Selected = false;
            }
        }

        public void Focus() {
            if (TryGetSelectionBounds(out var bounds)) {
                OrbitCameraSystem.Focus(bounds);
            }
        }

        private void OnFocusIn(FocusInEvent evt) {
            EditOperations.SetActiveHandler(this);
        }

        private void OnFocusOut(FocusOutEvent evt) {
            EditOperations.SetActiveHandler(null);
        }

        protected override void OnUpdate() { }
    }
}
