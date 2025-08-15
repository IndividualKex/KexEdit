using Unity.Entities;

namespace KexEdit {
    public struct Anchor : IComponentData {
        public PointData Value;

        public static implicit operator PointData(Anchor anchor) => anchor.Value;
        public static implicit operator Anchor(PointData point) => new() { Value = point };
    }
}
