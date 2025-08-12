using Unity.Entities;

namespace KexEdit.UI {
    public class CartStyle : IComponentData {
        public Entity Mesh;
        public string MeshPath;
        public int Version;
        public bool Loaded;
    }
}
