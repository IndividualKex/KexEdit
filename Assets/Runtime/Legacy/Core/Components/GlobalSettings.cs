using UnityEngine;
using Unity.Entities;

namespace KexEdit.Legacy {
    public class GlobalSettings : IComponentData {
        public ComputeShader Compute;
        public Material DuplicationMaterial;
        public Material ExtrusionMaterial;
        public Material GizmoMaterial;
    }
}
