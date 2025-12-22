using KexEdit.Legacy;
using KexEdit.Sim.Schema;
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
            var currentMode = Preferences.VisualizationMode;

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
                    _data.GradientType = VisualizationGradientType.TwoColorPositive;
                    _data.NeutralOffset = 0f;
                    break;
                case VisualizationMode.NormalForce:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Normal Force";
                    _data.GradientType = VisualizationGradientType.ThreeColorCrossesZero;
                    _data.NeutralOffset = 1f;
                    break;
                case VisualizationMode.LateralForce:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Lateral Force";
                    _data.GradientType = VisualizationGradientType.ThreeColorCrossesZero;
                    _data.NeutralOffset = 0f;
                    break;
                case VisualizationMode.RollSpeed:
                    _data.IsVisible = true;
                    _data.VisualizationName = "Roll Speed";
                    _data.GradientType = VisualizationGradientType.ThreeColorCrossesZero;
                    _data.NeutralOffset = 0f;
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
