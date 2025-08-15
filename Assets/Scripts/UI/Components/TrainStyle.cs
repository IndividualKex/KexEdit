using Unity.Entities;

namespace KexEdit.UI {
    public class TrainStyle : IComponentData {
        public Entity Mesh;
        public string MeshPath;
        public int Version;
        public bool Loaded;
    }
}
