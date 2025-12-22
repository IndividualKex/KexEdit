using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    public struct AppendReference : IComponentData {
        public Entity Value;
        public FixedString512Bytes FilePath;
        public bool Loaded;
    }
}
