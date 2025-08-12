using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    public struct MeshReference : IComponentData {
        public Entity Value;
        public FixedString512Bytes FilePath;
        public bool Loaded;
    }
}
