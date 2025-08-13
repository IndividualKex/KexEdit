using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class GizmoSettings : IComponentData {
        public List<ExtrusionGizmoSettings> ExtrusionGizmos = new();
    }
}
