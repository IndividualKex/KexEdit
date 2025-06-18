using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class GlobalMeshData : IComponentData {
        public ComputeShader Compute;
        public List<DuplicationMeshSettings> DuplicationMeshes = new();
        public List<ExtrusionMeshSettings> ExtrusionMeshes = new();
        public List<DuplicationGizmoSettings> DuplicationGizmos = new();
        public List<ExtrusionGizmoSettings> ExtrusionGizmos = new();
        public Color SelectedColor;
    }
}
