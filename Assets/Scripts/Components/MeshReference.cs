using Unity.Collections;
using Unity.Entities;

namespace KexEdit {
    public class MeshReference : IComponentData {
        public NodeMesh Value;
        public FixedString512Bytes FilePath;
        public bool Loaded;
    }
}
