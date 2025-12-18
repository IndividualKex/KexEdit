using Unity.Entities;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UISimulationSystemGroup : ComponentSystemGroup {
        protected override void OnCreate() {
            base.OnCreate();
            Enabled = false;
        }
    }
}
