using Unity.Entities;

namespace KexEdit {
    public class LoadTrackStyleSettingsEvent : IComponentData {
        public TrackStyleData Data;
    }
}
