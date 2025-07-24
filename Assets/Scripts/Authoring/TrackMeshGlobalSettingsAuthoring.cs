using UnityEngine;
using Unity.Entities;

namespace KexEdit {
    public class TrackMeshGlobalSettingsAuthoring : MonoBehaviour {
        public ComputeShader TrackMeshCompute;
        public DuplicationGizmoData[] DuplicationGizmos;
        public ExtrusionGizmoData[] ExtrusionGizmos;
        public Material DuplicationMaterial;
        public Material ExtrusionMaterial;
        public Material DuplicationGizmoMaterial;
        public Material ExtrusionGizmoMaterial;
        public Color SelectedColor;

        private class Baker : Baker<TrackMeshGlobalSettingsAuthoring> {
            public override void Bake(TrackMeshGlobalSettingsAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponentObject(entity, new TrackMeshGlobalSettings {
                    Compute = authoring.TrackMeshCompute,
                    DuplicationMaterial = authoring.DuplicationMaterial,
                    ExtrusionMaterial = authoring.ExtrusionMaterial,
                    DuplicationGizmoMaterial = authoring.DuplicationGizmoMaterial,
                    ExtrusionGizmoMaterial = authoring.ExtrusionGizmoMaterial,
                    SelectedColor = authoring.SelectedColor,
                });

                AddComponentObject(entity, new TrackStyleSettings());

                var gizmoSettings = new TrackMeshGizmoSettings();

                if (authoring.DuplicationGizmos != null) {
                    foreach (var duplicationGizmo in authoring.DuplicationGizmos) {
                        var material = new Material(authoring.DuplicationGizmoMaterial);
                        material.SetColor("_Color", duplicationGizmo.Color);

                        gizmoSettings.DuplicationGizmos.Add(new DuplicationGizmoSettings {
                            Material = material,
                            StartHeart = duplicationGizmo.StartHeart,
                            EndHeart = duplicationGizmo.EndHeart
                        });
                    }
                }

                if (authoring.ExtrusionGizmos != null) {
                    foreach (var extrusionGizmo in authoring.ExtrusionGizmos) {
                        var material = new Material(authoring.ExtrusionGizmoMaterial);
                        material.SetColor("_Color", extrusionGizmo.Color);

                        gizmoSettings.ExtrusionGizmos.Add(new ExtrusionGizmoSettings {
                            Material = material,
                            Heart = extrusionGizmo.Heart
                        });
                    }
                }

                AddComponentObject(entity, gizmoSettings);
            }
        }

        [System.Serializable]
        public class DuplicationGizmoData {
            public float StartHeart;
            public float EndHeart;
            public Color Color = Color.white;
        }

        [System.Serializable]
        public class ExtrusionGizmoData {
            public float Heart;
            public Color Color = Color.white;
        }
    }
}
