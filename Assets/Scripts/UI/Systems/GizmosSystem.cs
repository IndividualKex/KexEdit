using Unity.Entities;

namespace KexEdit.UI {
    public partial class GizmosSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<Gizmos>();
        }

        protected override void OnUpdate() {
            ref var gizmos = ref SystemAPI.GetSingletonRW<Gizmos>().ValueRW;
            gizmos.DrawGizmos = Preferences.ShowGizmos && !OrbitCameraSystem.IsRideCameraActive;
        }
    }
}
