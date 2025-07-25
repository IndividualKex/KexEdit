using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIInitializationSystemGroup))]
    public partial class ProjectInitializationSystem : SystemBase {
        private bool _initialized = false;

        protected override void OnUpdate() {
            if (_initialized || !SystemAPI.HasSingleton<CameraState>()) return;

            _initialized = true;

            ProjectOperations.RecoverLastSession();
        }
    }
}
