using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class GlobalMeshDataAuthoring : MonoBehaviour {
        public ComputeShader TrackMeshCompute;
        public List<DuplicationMeshSettings> DuplicationMeshes;
        public List<ExtrusionMeshSettings> ExtrusionMeshes;
        public List<DuplicationGizmoSettings> DuplicationGizmos;
        public List<ExtrusionGizmoSettings> ExtrusionGizmos;
        public Color SelectedColor;

        private class Baker : Baker<GlobalMeshDataAuthoring> {
            public override void Bake(GlobalMeshDataAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponentObject(entity, new GlobalMeshData {
                    Compute = authoring.TrackMeshCompute,
                    DuplicationMeshes = authoring.DuplicationMeshes,
                    ExtrusionMeshes = authoring.ExtrusionMeshes,
                    DuplicationGizmos = authoring.DuplicationGizmos,
                    ExtrusionGizmos = authoring.ExtrusionGizmos,
                    SelectedColor = authoring.SelectedColor,
                });
            }
        }
    }
}
