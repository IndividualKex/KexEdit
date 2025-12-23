using UnityEngine;

namespace KexEdit.UI.Timeline {
    public static class Constants {
        public static readonly Color s_RollSpeedCurveColor = new(0.85f, 0.4f, 0.4f);        // Red
        public static readonly Color s_NormalForceCurveColor = new(0.4f, 0.6f, 0.95f);      // Blue
        public static readonly Color s_LateralForceCurveColor = new(0.4f, 0.85f, 0.4f);     // Green
        public static readonly Color s_PitchSpeedCurveColor = new(0.4f, 0.8f, 0.85f);       // Cyan
        public static readonly Color s_YawSpeedCurveColor = new(0.8f, 0.8f, 0.4f);          // Yellow
        public static readonly Color s_DefaultCurveColor = new(0.8f, 0.8f, 0.8f);           // White

        public static readonly string s_RollSpeedName = "Roll Speed";
        public static readonly string s_NormalForceName = "Normal Force";
        public static readonly string s_LateralForceName = "Lateral Force";
        public static readonly string s_PitchSpeedName = "Pitch Speed";
        public static readonly string s_YawSpeedName = "Yaw Speed";
        public static readonly string s_FixedVelocityName = "Fixed Velocity";
        public static readonly string s_HeartName = "Heart";
        public static readonly string s_FrictionName = "Friction";
        public static readonly string s_ResistanceName = "Resistance";
        public static readonly string s_TrackStyleName = "Track Style";

        public const float RESOLUTION = 128f;
        public const float LEFT_PADDING = 32f;
        public const float ROW_HEIGHT = 20f;
        public const float TICK_SPACING = 12.8f;
        public const float MIN_MAJOR_SPACING = 50f;
        public const float VALUE_PADDING = 20f;
        public const float KEYFRAME_SIZE = 8f;
        public const float CURVE_WIDTH = 2f;
        public const float HANDLE_SIZE = 8f;
        public const float HANDLE_LINE_WIDTH = 1f;
        public const float MAJOR_HEIGHT = 12f;
        public const float MINOR_HEIGHT = 3f;
        public const float ZOOM_SPEED = 0.02f;
        public const float MIN_ZOOM = 0.01f;
        public const float MAX_ZOOM = 10f;
        public const float SNAPPING = 0.1f;
        public const float MIN_OPACITY = 0.3f;
        public const float DURATION_HANDLE_TOLERANCE = 12f;
        public const float PAN_SPEED = 0.05f;
        public const int CURVE_SEGMENTS = 50;
        public const int MAJOR_INTERVAL = 5;
    }
}
