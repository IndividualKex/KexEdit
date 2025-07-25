using Unity.Entities;

namespace KexEdit {
    public class LoadTrackStyleEvent : IComponentData {
        public TrackLoader.TrackStyleData TrackStyle;
    }
}
