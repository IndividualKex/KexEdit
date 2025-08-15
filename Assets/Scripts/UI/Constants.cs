using UnityEngine;

namespace KexEdit.UI {
    public static class Constants {
        public static readonly Color s_BackgroundColor = new(0.3f, 0.3f, 0.3f);
        public static readonly Color s_AltBackgroundColor = new(0.25f, 0.25f, 0.25f);
        public static readonly Color s_DarkBackgroundColor = new(0.2f, 0.2f, 0.2f);
        public static readonly Color s_AltDarkBackgroundColor = new(0.18f, 0.18f, 0.18f);
        public static readonly Color s_DividerColor = new(0.15f, 0.15f, 0.15f);
        public static readonly Color s_HoverColor = new(0.35f, 0.35f, 0.35f);
        public static readonly Color s_ActiveColor = new(0.4f, 0.4f, 0.4f);
        public static readonly Color s_ActiveHoverColor = new(0.5f, 0.5f, 0.5f);
        public static readonly Color s_BorderColor = new(0.1f, 0.1f, 0.1f);
        public static readonly Color s_MutedTextColor = new(0.6f, 0.6f, 0.6f);
        public static readonly Color s_TextColor = new(0.7f, 0.7f, 0.7f);
        public static readonly Color s_StatsRowColor1 = new(0.75f, 0.75f, 0.75f);
        public static readonly Color s_StatsRowColor2 = new(0.65f, 0.65f, 0.65f);
        public static readonly Color s_ActiveTextColor = new(0.8f, 0.8f, 0.8f);
        public static readonly Color s_ActiveTextColorTransparent = new(0.8f, 0.8f, 0.8f, 0.7f);
        public static readonly Color s_DarkenColor = new(0f, 0f, 0f, 0.3f);
        public static readonly Color s_MajorGridColor = new(0.8f, 0.8f, 0.8f, 0.03f);
        public static readonly Color s_MinorGridColor = new(0.8f, 0.8f, 0.8f, 0.02f);
        public static readonly Color s_MutedGridColor = new(0.8f, 0.8f, 0.8f, 0.01f);

        public static readonly Color s_RollSpeedColor = new(1.0f, 0.1f, 0.1f);
        public static readonly Color s_NormalForceColor = new(0.1f, 0.3f, 1.0f);
        public static readonly Color s_LateralForceColor = new(0.1f, 1.0f, 0.1f);
        public static readonly Color s_PitchSpeedColor = new(0.1f, 0.8f, 1.0f);
        public static readonly Color s_YawSpeedColor = new(1.0f, 0.9f, 0.1f);

        public static readonly Color s_StatsRollSpeedColor = new(0.75f, 0.65f, 0.65f);
        public static readonly Color s_StatsNormalForceColor = new(0.65f, 0.7f, 0.75f);
        public static readonly Color s_StatsLateralForceColor = new(0.65f, 0.75f, 0.65f);
        public static readonly Color s_StatsPitchSpeedColor = new(0.65f, 0.75f, 0.75f);
        public static readonly Color s_StatsYawSpeedColor = new(0.75f, 0.75f, 0.65f);

        public static readonly Color s_BlueOutline = new(0.2f, 0.5f, 0.9f);
        public static readonly Color s_BlueOutlineTransparent = new(0.2f, 0.5f, 0.9f, 0.5f);
        public static readonly Color s_OrangeOutline = new(0.9f, 0.6f, 0.2f);
        public static readonly Color s_YellowOutline = new(0.8f, 0.7f, 0.2f);

        public static readonly string s_UnitsSeconds = "s";
        public static readonly string s_UnitsMeters = "m";
        public static readonly string s_UnitsFeet = "ft";
        public static readonly string s_UnitsRadians = "rad";
        public static readonly string s_UnitsDegrees = "deg";
        public static readonly string s_UnitsMetersPerSecond = "m/s";
        public static readonly string s_UnitsKilometersPerHour = "km/h";
        public static readonly string s_UnitsMilesPerHour = "mph";
        public static readonly string s_UnitsRadiansPerSecond = "rad/s";
        public static readonly string s_UnitsDegreesPerSecond = "deg/s";
        public static readonly string s_UnitsRadiansPerMeter = "rad/m";
        public static readonly string s_UnitsRadiansPerFoot = "rad/ft";
        public static readonly string s_UnitsDegreesPerMeter = "deg/m";
        public static readonly string s_UnitsDegreesPerFoot = "deg/ft";
        public static readonly string s_UnitsGs = "g's";
        public static readonly string s_UnitsOneOverMicrometers = "1/μm";

        public static readonly string s_UnitsSuffixSeconds = "(s)";
        public static readonly string s_UnitsSuffixMeters = "(m)";
        public static readonly string s_UnitsSuffixFeet = "(ft)";
        public static readonly string s_UnitsSuffixRadians = "(rad)";
        public static readonly string s_UnitsSuffixDegrees = "(deg)";
        public static readonly string s_UnitsSuffixMetersPerSecond = "(m/s)";
        public static readonly string s_UnitsSuffixKilometersPerHour = "(km/h)";
        public static readonly string s_UnitsSuffixMilesPerHour = "(mph)";
        public static readonly string s_UnitsSuffixRadiansPerSecond = "(rad/s)";
        public static readonly string s_UnitsSuffixDegreesPerSecond = "(deg/s)";
        public static readonly string s_UnitsSuffixRadiansPerMeter = "(rad/m)";
        public static readonly string s_UnitsSuffixRadiansPerFoot = "(rad/ft)";
        public static readonly string s_UnitsSuffixDegreesPerMeter = "(deg/m)";
        public static readonly string s_UnitsSuffixDegreesPerFoot = "(deg/ft)";
        public static readonly string s_UnitsSuffixGs = "(g's)";
        public static readonly string s_UnitsSuffixOneOverMicrometers = "(1/μm)";

        public const float NODE_GRID_SIZE = 24f;
        public const float DEFAULT_RIDE_CAMERA_POSITION_X = 0f;
        public const float DEFAULT_RIDE_CAMERA_POSITION_Y = 1.1f;
        public const float DEFAULT_RIDE_CAMERA_POSITION_Z = 0f;
        public const float DEFAULT_RIDE_CAMERA_ROTATION_X = 0f;
        public const float DEFAULT_RIDE_CAMERA_ROTATION_Y = 0f;
        public const float DEFAULT_RIDE_CAMERA_ROTATION_Z = 0f;
    }
}
