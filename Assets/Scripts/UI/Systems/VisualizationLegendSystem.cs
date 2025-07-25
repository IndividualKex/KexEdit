using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class VisualizationLegendSystem : SystemBase {
        private VisualizationLegendData _data;
        private VisualizationLegend _legend;
        private VisualizationMode _lastMode = VisualizationMode.None;

        protected override void OnStartRunning() {
            _data = new VisualizationLegendData {
                VisualizationName = "",
                UnitsString = "",
                MinValue = 0f,
                MaxValue = 1f,
                IsVisible = false
            };

            var root = UIService.Instance.UIDocument.rootVisualElement;
            var gameView = root.Q<GameView>();

            _legend = new VisualizationLegend(_data);
            gameView?.Add(_legend);
        }

        protected override void OnUpdate() {
            var currentMode = VisualizationSystem.CurrentMode;

            if (currentMode != _lastMode) {
                _lastMode = currentMode;
                UpdateLegendForMode(currentMode);
            }

            if (currentMode != VisualizationMode.None) {
                UpdateUnitsAndValues(currentMode);
            }
        }

        private void UpdateLegendForMode(VisualizationMode mode) {
            switch (mode) {
                case VisualizationMode.None:
                    _data.IsVisible = false;
                    break;
                case VisualizationMode.Velocity:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Velocity";
                    break;
                case VisualizationMode.NormalForce:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Normal Force";
                    break;
                case VisualizationMode.LateralForce:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Lateral Force";
                    break;
                case VisualizationMode.RollSpeed:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Roll Speed";
                    break;
                case VisualizationMode.PitchSpeed:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Pitch Speed";
                    break;
                case VisualizationMode.YawSpeed:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Yaw Speed";
                    break;
                case VisualizationMode.Curvature:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Curvature";
                    break;
            }
        }

        private void UpdateUnitsAndValues(VisualizationMode mode) {
            switch (mode) {
                case VisualizationMode.Velocity:
                    _data.UnitsString = Units.GetSpeedUnitsString();
                    break;
                case VisualizationMode.NormalForce:
                case VisualizationMode.LateralForce:
                    _data.UnitsString = "G";
                    break;
                case VisualizationMode.RollSpeed:
                case VisualizationMode.PitchSpeed:
                case VisualizationMode.YawSpeed:
                case VisualizationMode.Curvature:
                    _data.UnitsString = Units.GetAnglePerTimeString();
                    break;
            }

            float minValue = Shader.GetGlobalFloat("_MinValue");
            float maxValue = Shader.GetGlobalFloat("_MaxValue");

            switch (mode) {
                case VisualizationMode.Velocity:
                    _data.MinValue = Units.SpeedToDisplay(minValue);
                    _data.MaxValue = Units.SpeedToDisplay(maxValue);
                    break;
                case VisualizationMode.RollSpeed:
                case VisualizationMode.PitchSpeed:
                case VisualizationMode.YawSpeed:
                case VisualizationMode.Curvature:
                    _data.MinValue = Units.AnglePerTimeToDisplay(minValue);
                    _data.MaxValue = Units.AnglePerTimeToDisplay(maxValue);
                    break;
                default:
                    _data.MinValue = minValue;
                    _data.MaxValue = maxValue;
                    break;
            }
        }
    }
}
