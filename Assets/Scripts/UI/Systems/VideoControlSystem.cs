using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class VideoControlSystem : SystemBase {
        private VideoControlData _data;
        private VideoControls _videoControls;

        protected override void OnStartRunning() {
            var root = UIService.Instance.UIDocument.rootVisualElement;
            var gameView = root.Q<VisualElement>("GameView");

            _data = new VideoControlData { IsPlaying = !KexTime.IsPaused };

            _videoControls = new VideoControls(_data);
            gameView.Add(_videoControls);

            _videoControls.TogglePlayPause += TogglePlayPause;
            _videoControls.SetProgress += SetProgress;
        }

        protected override void OnUpdate() {
            _videoControls.Draw();

            if (Keyboard.current.spaceKey.wasPressedThisFrame) {
                TogglePlayPause();
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
                if (cart.ValueRO.Active) {
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
