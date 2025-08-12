using UnityEngine;
using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public class DuplicationGizmoSettings : IComponentData {
        public Material Material;
        public float StartHeart;
        public float EndHeart;
    }
}
