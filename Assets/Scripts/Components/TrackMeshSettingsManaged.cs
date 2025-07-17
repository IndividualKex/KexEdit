using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class TrackMeshSettingsManaged : IComponentData {
        public List<DuplicationMeshSettings> DuplicationMeshes = new();
        public List<ExtrusionMeshSettings> ExtrusionMeshes = new();
    }
}
