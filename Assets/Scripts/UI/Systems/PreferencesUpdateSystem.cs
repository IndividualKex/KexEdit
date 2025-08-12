using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class PreferencesUpdateSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<KexEdit.Preferences>();
        }

        protected override void OnUpdate() {
            ref var gizmos = ref SystemAPI.GetSingletonRW<KexEdit.Preferences>().ValueRW;

            gizmos.VelocityRange = Preferences.GetVisualizationRange(VisualizationMode.Velocity);
            gizmos.NormalForceRange = Preferences.GetVisualizationRange(VisualizationMode.NormalForce);
            gizmos.LateralForceRange = Preferences.GetVisualizationRange(VisualizationMode.LateralForce);
            gizmos.RollSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.RollSpeed);
            gizmos.PitchSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.PitchSpeed);
            gizmos.YawSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.YawSpeed);
            gizmos.CurvatureRange = Preferences.GetVisualizationRange(VisualizationMode.Curvature);
            gizmos.VisualizationMode = Preferences.VisualizationMode;
            gizmos.DrawGizmos = Preferences.ShowGizmos && !OrbitCameraSystem.IsRideCameraActive;
        }
    }
}
