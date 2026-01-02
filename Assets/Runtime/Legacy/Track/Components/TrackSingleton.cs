using Unity.Entities;
using TrackData = KexEdit.Track.Track;

namespace KexEdit.Legacy {
    public struct TrackSingleton : IComponentData {
        public TrackData Value;
    }
}
