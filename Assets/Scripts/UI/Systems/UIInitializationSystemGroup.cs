using Unity.Entities;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class UIInitializationSystemGroup : ComponentSystemGroup {
        protected override void OnCreate() {
            base.OnCreate();
            Enabled = false;
        }
    }
}
