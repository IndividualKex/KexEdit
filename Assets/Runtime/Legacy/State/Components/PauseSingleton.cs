using Unity.Entities;

namespace KexEdit.Legacy {
    public struct PauseSingleton : IComponentData {
        public bool IsPaused;
    }
}
