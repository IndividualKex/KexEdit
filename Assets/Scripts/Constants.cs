namespace KexEdit {
    public static class Constants {
        public const float M = 1f; // Mass in KG
        public const float G = 9.80665f; // Gravity in m/s^2
        public const float HZ = 100f; // Simulation rate in Hz
        public const float EPSILON = 1.192092896e-07f; // Epsilon for floating point comparisons
        public const float HEART_BASE = 1.1f; // Distance from track to rider heart in meters
        public const float FRICTION_BASE = 0.021f; // Friction coefficient
        public const float RESISTANCE_BASE = 2e-5f; // Air resistance coefficient
        public const float FRONT_WHEEL_OFFSET = 0.75f;
        public const float REAR_WHEEL_OFFSET = -0.75f;
        public const float MIN_VELOCITY = 1e-3f;
        public const float TRACK_POINT_HZ = 0.4f; // Distance between track points in meters
        public const int TIE_SPACING = 2; // Track point interval between ties
    }
}
