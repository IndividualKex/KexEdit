using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class VisualizationSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<Preferences>();
        }

        protected override void OnStartRunning() {
            Shader.SetGlobalColor("_MinColor", Constants.VISUALIZATION_MIN_COLOR);
            Shader.SetGlobalColor("_MaxColor", Constants.VISUALIZATION_MAX_COLOR);
            Shader.SetGlobalColor("_ZeroColor", Constants.VISUALIZATION_ZERO_COLOR);
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
