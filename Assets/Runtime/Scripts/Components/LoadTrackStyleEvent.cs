using Unity.Entities;

namespace KexEdit {
    public class LoadTrackStyleSettingsEvent : IComponentData {
        public TrackStyleSettingsData Data;
    }
}
