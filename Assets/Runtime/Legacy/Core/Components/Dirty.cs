using Unity.Entities;

namespace KexEdit.Legacy {
    /// <summary>
    /// Marks an entity as modified by UI systems.
    /// Used to track changes made through the UI that need to be synced to the Coaster aggregate.
    /// </summary>
    public struct Dirty : IComponentData, IEnableableComponent { }
}
