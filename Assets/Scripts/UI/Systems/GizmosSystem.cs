using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class GizmosSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<Gizmos>();
        }

        protected override void OnUpdate() {
            ref var gizmos = ref SystemAPI.GetSingletonRW<Gizmos>().ValueRW;

            gizmos.VelocityRange = Preferences.GetVisualizationRange(VisualizationMode.Velocity);
            gizmos.NormalForceRange = Preferences.GetVisualizationRange(VisualizationMode.NormalForce);
            gizmos.LateralForceRange = Preferences.GetVisualizationRange(VisualizationMode.LateralForce);
            gizmos.RollSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.RollSpeed);
            gizmos.PitchSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.PitchSpeed);
            gizmos.YawSpeedRange = Preferences.GetVisualizationRange(VisualizationMode.YawSpeed);
            gizmos.CurvatureRange = Preferences.GetVisualizationRange(VisualizationMode.Curvature);

            gizmos.DrawGizmos = Preferences.ShowGizmos && !OrbitCameraSystem.IsRideCameraActive;
        }
    }
}
