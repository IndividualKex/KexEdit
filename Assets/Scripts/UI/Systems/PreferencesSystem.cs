using Unity.Entities;

namespace KexEdit.UI {
    public partial class PreferencesSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<PreferencesSingleton>();
        }

        protected override void OnUpdate() {
            ref var preferences = ref SystemAPI.GetSingletonRW<PreferencesSingleton>().ValueRW;
            preferences.ShowGizmos = Preferences.ShowGizmos && !OrbitCameraSystem.IsRideCameraActive;
        }
    }
}
