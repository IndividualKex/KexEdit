using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class TrackMeshConfigManaged : IComponentData {
        public List<DuplicationMeshSettings> DuplicationMeshes = new();
        public List<ExtrusionMeshSettings> ExtrusionMeshes = new();
        public List<DuplicationGizmoSettings> DuplicationGizmos = new();
        public List<ExtrusionGizmoSettings> ExtrusionGizmos = new();
    }
}
