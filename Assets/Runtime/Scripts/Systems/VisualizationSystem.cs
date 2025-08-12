using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class VisualizationSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<Preferences>();
        }

        protected override void OnStartRunning() {
            Shader.SetGlobalColor("_MinColor", new Color(0f, 0f, 1f, 1f)); // Pure blue
            Shader.SetGlobalColor("_MaxColor", new Color(1f, 0f, 0f, 1f)); // Pure red
            Shader.SetGlobalColor("_ZeroColor", new Color(0.7f, 0.7f, 0.7f, 1f)); // Light gray
        }

        protected override void OnUpdate() {
            var preferences = SystemAPI.GetSingleton<Preferences>();
            VisualizationMode mode = preferences.VisualizationMode;

            Shader.SetGlobalColor("_SelectedColor", Constants.SELECTED_COLOR);
            Shader.SetGlobalFloat("_VisualizationMode", (float)mode);

            UpdateVisualization(mode);
        }

        private void UpdateVisualization(VisualizationMode mode) {
            if (mode == VisualizationMode.None) {
                Shader.SetGlobalFloat("_MinValue", 0f);
                Shader.SetGlobalFloat("_MaxValue", 1f);
                return;
            }

            var preferences = SystemAPI.GetSingleton<Preferences>();
            var range = mode switch {
                VisualizationMode.Velocity => preferences.VelocityRange,
                VisualizationMode.NormalForce => preferences.NormalForceRange,
                VisualizationMode.LateralForce => preferences.LateralForceRange,
                VisualizationMode.RollSpeed => preferences.RollSpeedRange,
                VisualizationMode.PitchSpeed => preferences.PitchSpeedRange,
                VisualizationMode.YawSpeed => preferences.YawSpeedRange,
                VisualizationMode.Curvature => preferences.CurvatureRange,
                _ => new float2(0f, 1f)
            };
            Shader.SetGlobalFloat("_MinValue", range.x);
            Shader.SetGlobalFloat("_MaxValue", range.y);
        }
    }
}
