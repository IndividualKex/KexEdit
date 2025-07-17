using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class TrackMeshGizmoSettings : IComponentData {
        public List<DuplicationGizmoSettings> DuplicationGizmos = new();
        public List<ExtrusionGizmoSettings> ExtrusionGizmos = new();
    }
}