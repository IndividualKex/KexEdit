using Unity.Entities;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class CleanupSystemGroup : ComponentSystemGroup { }
}
