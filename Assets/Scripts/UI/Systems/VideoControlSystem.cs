using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup), OrderLast = true)]
    public partial class VideoControlSystem : SystemBase {
        public static VideoControlSystem Instance { get; private set; }

        private const float ControlsHideDelay = 3f;

        private VideoControlData _data;
        private VideoControls _videoControls;
        private Vector2 _lastMousePosition;

        public static bool IsFullscreen => Instance?._data.IsFullscreen ?? false;

        public VideoControlSystem() {
            Instance = this;
        }

        protected override void OnStartRunning() {
            var root = UIService.Instance.UIDocument.rootVisualElement;
            var gameView = root.Q<GameView>();

            _data = new VideoControlData { IsPlaying = !KexTime.IsPaused };

            _videoControls = new VideoControls(_data);
            gameView.Add(_videoControls);

            _lastMousePosition = Mouse.current.position.ReadValue();

            _videoControls.TogglePlayPause += TogglePlayPause;
            _videoControls.SetProgress += SetProgress;
            _videoControls.ToggleFullscreen += ToggleFullscreen;
        }

        protected override void OnUpdate() {
            _videoControls.Draw();

            UpdateMouseTracking();

            if (Keyboard.current.spaceKey.wasPressedThisFrame) {
                TogglePlayPause();
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame && _data.IsFullscreen) {
                ToggleFullscreen();
            }

            var rootEntity = SystemAPI.HasSingleton<NodeGraphRoot>() ?
                SystemAPI.GetSingleton<NodeGraphRoot>().Value :
                Entity.Null;

            if (rootEntity == Entity.Null) {
                _data.TotalLength = 0f;
                _data.Progress = 0f;
                return;
            }

            CalculateTotalLength(rootEntity);

            if (_data.TotalLength <= 0f) return;

            if (!GetActiveCart(out var cartEntity, out var cart) || cart.Section == Entity.Null) {
                _data.Progress = 0f;
                return;
            }

            float currentDistance = CalculateDistanceToSection(rootEntity, cart.Section) + cart.Position;
            _data.Progress = _data.TotalLength > 0f ? currentDistance / _data.TotalLength : 0f;
        }

        private void TogglePlayPause() {
            _data.IsPlaying = !_data.IsPlaying;
            if (_data.IsPlaying) {
                KexTime.Unpause();
            }
            else {
                KexTime.Pause();
            }
        }

        private void UpdateMouseTracking() {
            Vector2 currentMousePosition = Mouse.current.position.ReadValue();
            bool hasMouseMoved = Vector2.Distance(currentMousePosition, _lastMousePosition) > 1f;

            if (hasMouseMoved) {
                _data.MouseIdleTimer = 0f;
                _data.IsControlsVisible = true;
                _lastMousePosition = currentMousePosition;

                if (_data.IsFullscreen) {
                    UnityEngine.Cursor.visible = true;
                }
            }
            else if (_data.IsFullscreen && !Mouse.current.leftButton.isPressed) {
                _data.MouseIdleTimer += UnityEngine.Time.unscaledDeltaTime;

                if (_data.MouseIdleTimer >= ControlsHideDelay) {
                    _data.IsControlsVisible = false;
                    UnityEngine.Cursor.visible = false;
                }
            }

            if (!_data.IsFullscreen) {
                _data.IsControlsVisible = true;
                _data.MouseIdleTimer = 0f;
                UnityEngine.Cursor.visible = true;
            }
        }

        public void ToggleFullscreen() {
            _data.IsFullscreen = !_data.IsFullscreen;

            var root = UIService.Instance.UIDocument.rootVisualElement;
            var topPanel = root.Q<VisualElement>("Top");
            var topLeftPanel = root.Q<VisualElement>("TopLeftPanel");
            var bottomPanel = root.Q<VisualElement>("Bottom");

            if (_data.IsFullscreen) {
                topPanel.style.display = DisplayStyle.None;
                topLeftPanel.style.display = DisplayStyle.None;
                bottomPanel.style.display = DisplayStyle.None;
                _data.IsControlsVisible = true;
                _data.MouseIdleTimer = 0f;
                UnityEngine.Cursor.visible = true;
                NotificationSystem.ShowNotification("To exit full screen, press Esc");
            }
            else {
                topPanel.style.display = DisplayStyle.Flex;
                topLeftPanel.style.display = DisplayStyle.Flex;
                bottomPanel.style.display = DisplayStyle.Flex;
                _data.IsControlsVisible = true;
                _data.MouseIdleTimer = 0f;
                UnityEngine.Cursor.visible = true;
            }
        }

        private void CalculateTotalLength(Entity startEntity) {
            _data.TotalLength = 0f;
            var currentEntity = startEntity;
            using var processedEntities = new NativeHashSet<Entity>(16, Allocator.Temp);

            while (currentEntity != Entity.Null && !processedEntities.Contains(currentEntity)) {
                processedEntities.Add(currentEntity);

                if (SystemAPI.HasBuffer<Point>(currentEntity)) {
                    _data.TotalLength += SystemAPI.GetBuffer<Point>(currentEntity).Length;
                }

                currentEntity = SystemAPI.HasComponent<Node>(currentEntity)
                    ? SystemAPI.GetComponent<Node>(currentEntity).Next
                    : Entity.Null;
            }
        }

        private float CalculateDistanceToSection(Entity startEntity, Entity targetSection) {
            float distance = 0f;
            var currentEntity = startEntity;
            using var processedEntities = new NativeHashSet<Entity>(16, Allocator.Temp);

            while (currentEntity != Entity.Null && !processedEntities.Contains(currentEntity)) {
                processedEntities.Add(currentEntity);

                if (currentEntity == targetSection) return distance;

                if (SystemAPI.HasBuffer<Point>(currentEntity)) {
                    distance += SystemAPI.GetBuffer<Point>(currentEntity).Length;
                }

                currentEntity = SystemAPI.HasComponent<Node>(currentEntity)
                    ? SystemAPI.GetComponent<Node>(currentEntity).Next
                    : Entity.Null;
            }

            return distance;
        }

        private bool GetActiveCart(out Entity cartEntity, out Cart cart) {
            foreach (var (cartComponent, entity) in SystemAPI.Query<Cart>().WithEntityAccess()) {
                if (cartComponent.Active && !cartComponent.Kinematic) {
                    cartEntity = entity;
                    cart = cartComponent;
                    return true;
                }
            }
            cartEntity = Entity.Null;
            cart = default;
            return false;
        }

        private void SetProgress(float progress) {
            _data.Progress = progress;

            var rootEntity = SystemAPI.HasSingleton<NodeGraphRoot>() ?
                SystemAPI.GetSingleton<NodeGraphRoot>().Value :
                Entity.Null;

            if (rootEntity == Entity.Null || _data.TotalLength <= 0f) return;

            var targetDistance = _data.Progress * _data.TotalLength;
            foreach (var (cart, entity) in SystemAPI.Query<RefRW<Cart>>().WithEntityAccess()) {
                if (cart.ValueRO.Active && !cart.ValueRO.Kinematic) {
                    SetCartPosition(ref cart.ValueRW, rootEntity, targetDistance);
                    break;
                }
            }
        }

        private void SetCartPosition(ref Cart cart, Entity startEntity, float targetDistance) {
            float currentDistance = 0f;
            Entity currentEntity = startEntity;
            var processedEntities = new NativeHashSet<Entity>(16, Allocator.Temp);

            while (currentEntity != Entity.Null && !processedEntities.Contains(currentEntity)) {
                processedEntities.Add(currentEntity);

                if (SystemAPI.HasBuffer<Point>(currentEntity)) {
                    var points = SystemAPI.GetBuffer<Point>(currentEntity);
                    float sectionLength = points.Length;

                    if (currentDistance + sectionLength >= targetDistance) {
                        cart.Section = currentEntity;
                        cart.Position = Mathf.Clamp(targetDistance - currentDistance, 0f, points.Length - 1f);
                        processedEntities.Dispose();
                        return;
                    }

                    currentDistance += sectionLength;
                }

                if (SystemAPI.HasComponent<Node>(currentEntity)) {
                    var node = SystemAPI.GetComponent<Node>(currentEntity);
                    currentEntity = node.Next;
                }
                else {
                    break;
                }
            }

            processedEntities.Dispose();

            if (cart.Section != Entity.Null && SystemAPI.HasBuffer<Point>(cart.Section)) {
                var points = SystemAPI.GetBuffer<Point>(cart.Section);
                cart.Position = points.Length - 1f;
            }
        }
    }
}
