using Unity.Entities;

namespace KexEdit.UI {
    public partial class GizmosSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<Gizmos>();
        }

        protected override void OnUpdate() {
            ref var preferences = ref SystemAPI.GetSingletonRW<Gizmos>().ValueRW;
            preferences.ShowGizmos = Preferences.ShowGizmos && !OrbitCameraSystem.IsRideCameraActive;
        }
    }
}
