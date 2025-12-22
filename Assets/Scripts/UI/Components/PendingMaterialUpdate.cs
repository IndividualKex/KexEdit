using Unity.Entities;
using UnityEngine;

namespace KexEdit.UI {
    public class PendingMaterialUpdate : IComponentData {
        public Material Material;
    }
}
