using UnityEngine;
using Unity.Entities;

namespace KexEdit {
    public class TrackMeshConfigAuthoring : MonoBehaviour {
        public ComputeShader TrackMeshCompute;
        public Material DuplicationMaterial;
        public Material ExtrusionMaterial;
        public Material DuplicationGizmoMaterial;
        public Material ExtrusionGizmoMaterial;
        public Color SelectedColor;

        private class Baker : Baker<TrackMeshConfigAuthoring> {
            public override void Bake(TrackMeshConfigAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponentObject(entity, new TrackMeshConfig {
                    Compute = authoring.TrackMeshCompute,
                    DuplicationMaterial = authoring.DuplicationMaterial,
                    ExtrusionMaterial = authoring.ExtrusionMaterial,
                    DuplicationGizmoMaterial = authoring.DuplicationGizmoMaterial,
                    ExtrusionGizmoMaterial = authoring.ExtrusionGizmoMaterial,
                    SelectedColor = authoring.SelectedColor,
                });
            }
        }
    }
}
