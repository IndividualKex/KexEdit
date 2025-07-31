namespace KexEdit.UI {
    public enum VisualizationGradientType {
        TwoColorPositive,
        ThreeColorCrossesZero
    }

    public class VisualizationLegendData {
        public string VisualizationName;
        public string UnitsString;
        public float MinValue;
        public float MaxValue;
        public VisualizationGradientType GradientType;
        public bool IsVisible;
    }
}
