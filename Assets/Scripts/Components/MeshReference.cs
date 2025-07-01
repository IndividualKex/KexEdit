using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    public class MeshReference : IComponentData {
        public FixedString512Bytes FilePath;
        public ManagedMesh Value;
        public bool Loaded;
    }
}
