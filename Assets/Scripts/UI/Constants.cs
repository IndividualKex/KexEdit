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
        public static readonly Color s_ActiveTextColor = new(0.8f, 0.8f, 0.8f);
        public static readonly Color s_ActiveTextColorTransparent = new(0.8f, 0.8f, 0.8f, 0.7f);
        public static readonly Color s_DarkenColor = new(0f, 0f, 0f, 0.3f);
        public static readonly Color s_MajorGridColor = new(0.8f, 0.8f, 0.8f, 0.03f);
        public static readonly Color s_MinorGridColor = new(0.8f, 0.8f, 0.8f, 0.02f);
        public static readonly Color s_MutedGridColor = new(0.8f, 0.8f, 0.8f, 0.01f);

        public static readonly Color s_BlueOutline = new(0.2f, 0.5f, 0.9f);
        public static readonly Color s_BlueOutlineTransparent = new(0.2f, 0.5f, 0.9f, 0.5f);
        public static readonly Color s_OrangeOutline = new(0.9f, 0.6f, 0.2f);
        public static readonly Color s_YellowOutline = new(0.8f, 0.7f, 0.2f);

        public static readonly string s_UnitsMeters = " m";
        public static readonly string s_UnitsFeet = " ft";
        public static readonly string s_UnitsRadians = " rad";
        public static readonly string s_UnitsSeconds = " s";
        public static readonly string s_UnitsMetersPerSecond = " m/s";
        public static readonly string s_UnitsRadiansPerSecond = " rad/s";
        public static readonly string s_UnitsRadiansPerMeter = " rad/m";
        public static readonly string s_UnitsGs = " g's";
        public static readonly string s_UnitsOneOverMicrometers = " 1/μm";

        public const float FRICTION_UI_TO_PHYSICS_SCALE = 1e-2f;
        public const float FRICTION_PHYSICS_TO_UI_SCALE = 1e2f;
        public const float RESISTANCE_UI_TO_PHYSICS_SCALE = 1e-6f;
        public const float RESISTANCE_PHYSICS_TO_UI_SCALE = 1e6f;
        public const float NODE_GRID_SIZE = 24f;
    }
}
