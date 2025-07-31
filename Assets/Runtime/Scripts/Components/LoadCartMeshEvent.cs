using Unity.Entities;
using UnityEngine;

namespace KexEdit {
    public class LoadCartMeshEvent : IComponentData {
        public Entity Target;
        public GameObject Cart;
    }
}
