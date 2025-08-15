using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class PreferencesUpdateSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<KexEdit.Preferences>();
        }

        protected override void OnUpdate() {
            ref var preferences = ref SystemAPI.GetSingletonRW<KexEdit.Preferences>().ValueRW;

            preferences.VelocityRange = Preferences.GetVisualizationRange(VisualizationMode.Velocity);
            preferences.NormalForceRange = Preferences.GetVisualizationRange(VisualizationMode.NormalForce);
            preferences.LateralForceRange = Preferences.GetVisualizationRange(VisualizationMode.LateralForce);
            preferences.RollSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.RollSpeed);
            preferences.PitchSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.PitchSpeed);
            preferences.YawSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.YawSpeed);
            preferences.CurvatureRange = Preferences.GetVisualizationRange(VisualizationMode.Curvature);
            preferences.VisualizationMode = Preferences.VisualizationMode;
            preferences.DrawGizmos = Preferences.ShowGizmos && !OrbitCameraSystem.IsRideCameraActive;
        }
    }
}
