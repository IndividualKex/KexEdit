using Unity.Entities;

namespace KexEdit {
    public struct DuplicationMeshSettings : IComponentData {
        public int Step;
        public int Offset;
    }
}
