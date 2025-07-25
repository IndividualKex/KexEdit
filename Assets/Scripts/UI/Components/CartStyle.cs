using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    public class CartStyle : IComponentData {
        public GameObject Mesh;
        public string MeshPath;
        public int Version;
        public bool Loaded;
    }
}
