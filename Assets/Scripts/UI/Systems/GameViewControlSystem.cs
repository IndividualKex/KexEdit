using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using KexEdit.UI.NodeGraph;
using UnityEngine;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class GameViewControlSystem : SystemBase, IEditableHandler {
        private UnityEngine.Camera _camera;
        private NodeGraphView _nodeGraphView;
        private GameView _gameView;

        public static GameViewControlSystem Instance { get; private set; }

        public GameViewControlSystem() {
            Instance = this;
        }

        protected override void OnStartRunning() {
            _camera = UnityEngine.Camera.main;

            var root = UIService.Instance.UIDocument.rootVisualElement;
            _nodeGraphView = root.Q<NodeGraphView>();
            _gameView = root.Q<GameView>();

            _gameView.RegisterCallback<MouseDownEvent>(OnGameViewMouseDown);

            EditOperationsSystem.RegisterHandler(this);
        }

        protected override void OnDestroy() {
            EditOperationsSystem.UnregisterHandler(this);
            base.OnDestroy();
        }

        private void OnGameViewMouseDown(MouseDownEvent evt) {
            bool altOrCmdPressed = Keyboard.current.leftAltKey.isPressed ||
                                  Keyboard.current.rightAltKey.isPressed ||
                                  Keyboard.current.leftCommandKey.isPressed ||
                                  Keyboard.current.rightCommandKey.isPressed;

            if (altOrCmdPressed || OrbitCameraSystem.IsRideCameraActive || evt.button != 0) return;

            bool shiftPressed = Keyboard.current.shiftKey.isPressed;
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
        public bool CanSelectAll() => false;
        public bool CanDeselectAll() => false;
        public bool CanFocus() => TryGetSelectionBounds(out _);

        public void Copy() { }
        public void Paste(float2? worldPosition = null) { }
        public void Delete() { }
        public void Cut() { }
        public void SelectAll() { }
        public void DeselectAll() { }

        public void Focus() {
            if (TryGetSelectionBounds(out var bounds)) {
                OrbitCameraSystem.Focus(bounds);
            }
        }

        public bool IsInBounds(Vector2 mousePosition) {
            return _gameView.worldBound.Contains(mousePosition);
        }

        protected override void OnUpdate() { }
    }
}
