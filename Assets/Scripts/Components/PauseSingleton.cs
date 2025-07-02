using Unity.Entities;

namespace KexEdit {
    public struct PauseSingleton : IComponentData {
        public bool IsPaused;
    }
}
