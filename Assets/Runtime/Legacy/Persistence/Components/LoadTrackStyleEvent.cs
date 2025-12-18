using Unity.Entities;

namespace KexEdit.Legacy {
    public class LoadTrackStyleSettingsEvent : IComponentData {
        public TrackStyleSettingsData Data;
    }
}
