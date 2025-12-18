using Unity.Entities;
using UnityEngine;

using KexEdit.Legacy;
namespace KexEdit.UI {
    public class PendingMaterialUpdate : IComponentData {
        public Material Material;
    }
}