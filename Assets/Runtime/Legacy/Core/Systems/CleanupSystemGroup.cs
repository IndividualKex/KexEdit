using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class CleanupSystemGroup : ComponentSystemGroup { }
}
