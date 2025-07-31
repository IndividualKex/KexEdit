using Unity.Entities;

namespace KexEdit {
    public class LoadTrackStyleEvent : IComponentData {
        public Entity Target;
        public TrackStyleData TrackStyle;
    }
}
