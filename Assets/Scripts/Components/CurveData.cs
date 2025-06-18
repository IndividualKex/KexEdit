using Unity.Entities;

namespace KexEdit {
    public struct CurveData : IComponentData {
        public float Radius;
        public float Arc;
        public float Axis;
        public float LeadIn;
        public float LeadOut;

        public static CurveData Default => new() {
            Radius = 10f,
            Arc = 90f,
            Axis = 0f,
            LeadIn = 0f,
            LeadOut = 0f,
        };
    }
}
