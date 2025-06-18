using Unity.Entities;

namespace KexEdit {
    public struct AnchorPort : IComponentData {
        public PointData Value;

        public static implicit operator PointData(AnchorPort port) => port.Value;
        public static implicit operator AnchorPort(PointData value) => new() { Value = value };
    }
}
