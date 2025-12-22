using UnityEngine;
using System;
using Unity.Entities;

namespace KexEdit.Legacy {
    [Serializable]
    public class ExtrusionGizmoSettings : IComponentData {
        public Material Material;
        public float Heart;
    }
}
