using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit.Legacy {
    public class GizmoSettings : IComponentData {
        public List<ExtrusionGizmoSettings> ExtrusionGizmos = new();
    }
}
