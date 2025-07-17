namespace KexEdit {
    public static class Constants {
        public const float M = 1f; // Mass in KG
        public const float G = 9.80665f; // Gravity in m/s^2
        public const float HZ = 100f; // Simulation rate in Hz
        public const float EPSILON = 1.192092896e-07f; // Epsilon for floating point comparisons
        public const float HEART_BASE = 1.1f; // Distance from track to rider heart in meters
        public const float FRICTION_BASE = 0.021f; // Friction coefficient
        public const float RESISTANCE_BASE = 2e-5f; // Air resistance coefficient
        public const float MIN_VELOCITY = 1e-3f;
        public const float TORSION_STRESS_FACTOR = 0.1f;
        public const float MIN_CURVATURE = 0.001f;
        public const float MIN_SEGMENT_LENGTH = 15f;
        public const float MAX_SEGMENT_LENGTH = 30f;
        public const int STRESS_ROLLING_WINDOW = 50;
    }
}
