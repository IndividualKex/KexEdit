using UnityEngine;
using Unity.Entities;

namespace KexEdit {
    public class TrackMeshConfig : IComponentData {
        public ComputeShader Compute;
        public Material DuplicationMaterial;
        public Material ExtrusionMaterial;
        public Material DuplicationGizmoMaterial;
        public Material ExtrusionGizmoMaterial;
        public Color SelectedColor;
    }
}
