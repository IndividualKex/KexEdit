using KexEdit.Legacy;
namespace KexEdit.UI.NodeGraph {
    public struct PortBounds {
        public float Min;
        public float Max;

        public PortBounds(float min, float max) {
            Min = min;
            Max = max;
        }
    }
}
