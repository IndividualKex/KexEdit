using UnityEngine;
using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public class ExtrusionGizmoSettings : IComponentData {
        public Material Material;
        public float Heart;
    }
}
