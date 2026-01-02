using Unity.Entities;
using Unity.Collections;

namespace KexEdit.Legacy {
    public struct NodeMeshReference : IComponentData {
        public Entity Value;
        public FixedString512Bytes FilePath;
        public bool Requested;
    }
}
