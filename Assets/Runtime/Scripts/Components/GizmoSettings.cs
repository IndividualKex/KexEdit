using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class GizmoSettings : IComponentData {
        public List<DuplicationGizmoSettings> DuplicationGizmos = new();
        public List<ExtrusionGizmoSettings> ExtrusionGizmos = new();
        public bool EnableShadows;
    }
}
