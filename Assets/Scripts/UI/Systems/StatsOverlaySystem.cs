using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class StatsOverlaySystem : SystemBase {
        private VisualElement _statsOverlay;
        private Label _xLabel;
        private Label _yLabel;
        private Label _zLabel;
        private Label _rollLabel;
        private Label _pitchLabel;
        private Label _yawLabel;
        private Label _velocityLabel;
        private Label _normalForceLabel;
        private Label _lateralForceLabel;

        private bool _isVisible;
        private PointData _lastPoint;

        private static readonly string[] s_StringPool = new string[9];
        private static int s_PoolIndex;

        static StatsOverlaySystem() {
            for (int i = 0; i < s_StringPool.Length; i++) {
                s_StringPool[i] = new string('\0', 64);
            }
        }

        protected override void OnStartRunning() {
            var root = UIService.Instance.UIDocument.rootVisualElement;
            CreateStatsOverlay(root);
        }

        private void CreateStatsOverlay(VisualElement root) {
            _statsOverlay = new VisualElement {
                name = "stats-overlay",
                style = {
                    position = Position.Absolute,
                    top = 10f,
                    right = 10f,
                    minWidth = 256f,
                    backgroundColor = new Color(0, 0, 0, 0.2f),
                    borderTopLeftRadius = 4f,
                    borderTopRightRadius = 4f,
                    borderBottomLeftRadius = 4f,
                    borderBottomRightRadius = 4f,
                    paddingTop = 8f,
                    paddingBottom = 8f,
                    paddingLeft = 12f,
                    paddingRight = 12f,
                    display = DisplayStyle.None
                }
            };

            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column
                }
            };

            _xLabel = CreateStatLabel("X:");
            _yLabel = CreateStatLabel("Y:");
            _zLabel = CreateStatLabel("Z:");
            _rollLabel = CreateStatLabel("Roll:");
            _pitchLabel = CreateStatLabel("Pitch:");
            _yawLabel = CreateStatLabel("Yaw:");
            _velocityLabel = CreateStatLabel("Velocity:");
            _normalForceLabel = CreateStatLabel("Normal Force:");
            _lateralForceLabel = CreateStatLabel("Lateral Force:");

            container.Add(_xLabel);
            container.Add(_yLabel);
            container.Add(_zLabel);
            container.Add(_rollLabel);
            container.Add(_pitchLabel);
            container.Add(_yawLabel);
            container.Add(_velocityLabel);
            container.Add(_normalForceLabel);
            container.Add(_lateralForceLabel);

            _statsOverlay.Add(container);

            var gameView = root.Q<VisualElement>("GameView");
            gameView?.Add(_statsOverlay);
        }

        private Label CreateStatLabel(string labelText) {
            var label = new Label(labelText) {
                style = {
                    color = new Color(0.8f, 0.8f, 0.8f),
                    fontSize = 12,
                }
            };
            return label;
        }

        protected override void OnUpdate() {
            bool shouldShow = PreferencesSystem.ShowStats;

            if (shouldShow != _isVisible) {
                _isVisible = shouldShow;
                _statsOverlay.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!_isVisible) return;

            var (cartEntity, cart) = GetActiveCart();
            bool hasValidCart = cartEntity != Entity.Null &&
                               cart.Section != Entity.Null &&
                               SystemAPI.Exists(cart.Section) &&
                               SystemAPI.HasBuffer<Point>(cart.Section);

            if (!hasValidCart) {
                ShowNoStatsMessage();
                return;
            }

            var pointBuffer = SystemAPI.GetBuffer<Point>(cart.Section);
            if (pointBuffer.Length == 0) {
                ShowNoStatsMessage();
                return;
            }

            PointData currentPoint = GetInterpolatedPoint(pointBuffer, cart.Position);

            if (!PointsEqual(currentPoint, _lastPoint)) {
                _lastPoint = currentPoint;
                UpdateLabels(currentPoint);
            }
        }

        private (Entity cartEntity, Cart cart) GetActiveCart() {
            foreach (var (cartComponent, entity) in SystemAPI.Query<Cart>().WithEntityAccess()) {
                if (cartComponent.Active && !cartComponent.Kinematic) {
                    return (entity, cartComponent);
                }
            }
            return (Entity.Null, default);
        }

        private PointData GetInterpolatedPoint(DynamicBuffer<Point> points, float position) {
            position = math.clamp(position, 0f, points.Length - 1f);
            int frontIndex = (int)math.floor(position);
            float t = position - frontIndex;

            if (frontIndex >= points.Length - 1) {
                return points[^1].Value;
            }

            if (t < 0.001f) {
                return points[frontIndex].Value;
            }

            PointData frontPoint = points[frontIndex].Value;
            PointData backPoint = points[frontIndex + 1].Value;

            return new PointData {
                Position = math.lerp(frontPoint.Position, backPoint.Position, t),
                Direction = math.normalize(math.lerp(frontPoint.Direction, backPoint.Direction, t)),
                Lateral = math.normalize(math.lerp(frontPoint.Lateral, backPoint.Lateral, t)),
                Normal = math.normalize(math.lerp(frontPoint.Normal, backPoint.Normal, t)),
                Roll = math.lerp(frontPoint.Roll, backPoint.Roll, t),
                Velocity = math.lerp(frontPoint.Velocity, backPoint.Velocity, t),
                NormalForce = math.lerp(frontPoint.NormalForce, backPoint.NormalForce, t),
                LateralForce = math.lerp(frontPoint.LateralForce, backPoint.LateralForce, t),
                Energy = math.lerp(frontPoint.Energy, backPoint.Energy, t),
                Heart = math.lerp(frontPoint.Heart, backPoint.Heart, t),
                Friction = math.lerp(frontPoint.Friction, backPoint.Friction, t),
                Resistance = math.lerp(frontPoint.Resistance, backPoint.Resistance, t),
                Facing = frontPoint.Facing
            };
        }

        private bool PointsEqual(PointData a, PointData b) {
            return math.abs(a.Position.x - b.Position.x) < 0.01f &&
                   math.abs(a.Position.y - b.Position.y) < 0.01f &&
                   math.abs(a.Position.z - b.Position.z) < 0.01f &&
                   math.abs(a.Roll - b.Roll) < 0.1f &&
                   math.abs(a.Velocity - b.Velocity) < 0.01f &&
                   math.abs(a.NormalForce - b.NormalForce) < 0.001f &&
                   math.abs(a.LateralForce - b.LateralForce) < 0.001f;
        }

        private void ShowNoStatsMessage() {
            _xLabel.text = "No stats available";
            _yLabel.text = "";
            _zLabel.text = "";
            _rollLabel.text = "";
            _pitchLabel.text = "";
            _yawLabel.text = "";
            _velocityLabel.text = "";
            _normalForceLabel.text = "";
            _lateralForceLabel.text = "";
        }

        private unsafe void UpdateLabels(PointData point) {
            _xLabel.text = FormatValue("X: ", point.Position.x, "F2", " m");
            _xLabel.MarkDirtyRepaint();

            _yLabel.text = FormatValue("Y: ", point.Position.y, "F2", " m");
            _yLabel.MarkDirtyRepaint();

            _zLabel.text = FormatValue("Z: ", point.Position.z, "F2", " m");
            _zLabel.MarkDirtyRepaint();

            _rollLabel.text = FormatValue("Roll: ", point.Roll, "F1", "°");
            _rollLabel.MarkDirtyRepaint();

            _pitchLabel.text = FormatValue("Pitch: ", point.GetPitch(), "F1", "°");
            _pitchLabel.MarkDirtyRepaint();

            _yawLabel.text = FormatValue("Yaw: ", point.GetYaw(), "F1", "°");
            _yawLabel.MarkDirtyRepaint();

            _velocityLabel.text = FormatValue("Velocity: ", point.Velocity, "F2", " m/s");
            _velocityLabel.MarkDirtyRepaint();

            _normalForceLabel.text = FormatValue("Normal Force: ", point.NormalForce, "F2", " G");
            _normalForceLabel.MarkDirtyRepaint();

            _lateralForceLabel.text = FormatValue("Lateral Force: ", point.LateralForce, "F2", " G");
            _lateralForceLabel.MarkDirtyRepaint();
        }

        private unsafe string FormatValue(string prefix, float value, string format, string suffix = "") {
            string pooledString = s_StringPool[s_PoolIndex];
            s_PoolIndex = (s_PoolIndex + 1) % s_StringPool.Length;

            Span<char> buffer = stackalloc char[64];
            int pos = 0;

            prefix.AsSpan().CopyTo(buffer[pos..]);
            pos += prefix.Length;

            if (!value.TryFormat(buffer[pos..], out int charsWritten, format)) return $"{prefix}0.00{suffix}";
            pos += charsWritten;

            if (suffix.Length > 0) {
                suffix.AsSpan().CopyTo(buffer[pos..]);
                pos += suffix.Length;
            }

            fixed (char* pooledPtr = pooledString) {
                for (int i = 0; i < pos; i++) {
                    pooledPtr[i] = buffer[i];
                }
                pooledPtr[pos] = '\0';
            }

            return pooledString;
        }
    }
}
