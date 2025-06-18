namespace KexEdit.UI.NodeGraph {
    public struct PortBounds {
        public float Min;
        public float Max;
        public float Sensitivity;

        public PortBounds(float min, float max, float sensitivity) {
            Min = min;
            Max = max;
            Sensitivity = sensitivity;
        }
    }
}
