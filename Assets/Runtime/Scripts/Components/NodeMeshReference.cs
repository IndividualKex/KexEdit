using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    public struct NodeMeshReference : IComponentData {
        public Entity Value;
        public FixedString512Bytes FilePath;
        public bool Loaded;
    }
}
