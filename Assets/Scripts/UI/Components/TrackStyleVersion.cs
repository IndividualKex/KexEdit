using Unity.Entities;

namespace KexEdit.UI {
    public partial struct TrackStyleVersion : IComponentData {
        public int Value;

        public static implicit operator int(TrackStyleVersion version) => version.Value;
        public static implicit operator TrackStyleVersion(int value) => new() { Value = value };
    }
}
