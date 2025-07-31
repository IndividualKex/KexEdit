using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class UIFixedStepSimulationSystemGroup : ComponentSystemGroup {
        protected override void OnCreate() {
            base.OnCreate();
            Enabled = false;
        }
    }
}
