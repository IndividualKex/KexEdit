using UnityEngine;

namespace KexEdit.UI {
    public static class Preferences {
        private const string PREF_DISTANCE_UNITS = "DistanceUnits";
        private const string PREF_ANGLE_UNITS = "AngleUnits";
        private const string PREF_ANGLE_CHANGE_UNITS = "AngleChangeUnits";
        private const string PREF_SPEED_UNITS = "SpeedUnits";

        private const string PREF_NODE_GRID_SNAPPING = "NodeGridSnapping";
        private const string PREF_UI_SCALE = "UIScale";
        private const string PREF_SHOW_STATS = "ShowStats";

        private static DistanceUnitsType s_DistanceUnits;
        private static AngleUnitsType s_AngleUnits;
        private static AngleChangeUnitsType s_AngleChangeUnits;
        private static SpeedUnitsType s_SpeedUnits;

        private static float s_UIScale;
        private static bool s_NodeGridSnapping;
        private static bool s_ShowStats;

        static Preferences() {
            LoadPreferences();
        }

        public static DistanceUnitsType DistanceUnits {
            get => s_DistanceUnits;
            set {
                s_DistanceUnits = value;
                PlayerPrefs.SetInt(PREF_DISTANCE_UNITS, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static AngleUnitsType AngleUnits {
            get => s_AngleUnits;
            set {
                s_AngleUnits = value;
                PlayerPrefs.SetInt(PREF_ANGLE_UNITS, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static AngleChangeUnitsType AngleChangeUnits {
            get => s_AngleChangeUnits;
            set {
                s_AngleChangeUnits = value;
                PlayerPrefs.SetInt(PREF_ANGLE_CHANGE_UNITS, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static SpeedUnitsType SpeedUnits {
            get => s_SpeedUnits;
            set {
                s_SpeedUnits = value;
                PlayerPrefs.SetInt(PREF_SPEED_UNITS, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static float UIScale {
            get => s_UIScale;
            set {
                s_UIScale = value;
                PlayerPrefs.SetFloat(PREF_UI_SCALE, value);
                PlayerPrefs.Save();
            }
        }

        public static bool NodeGridSnapping {
            get => s_NodeGridSnapping;
            set {
                s_NodeGridSnapping = value;
                PlayerPrefs.SetInt(PREF_NODE_GRID_SNAPPING, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool ShowStats {
            get => s_ShowStats;
            set {
                s_ShowStats = value;
                PlayerPrefs.SetInt(PREF_SHOW_STATS, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float GetDefaultUIScale() {
            float dpi = Screen.dpi;
            if (dpi <= 96f) return 1f;
            if (dpi <= 144f) return 1.25f;
            if (dpi <= 192f) return 1.5f;
            return 2f;
        }

        private static void LoadPreferences() {
            s_DistanceUnits = (DistanceUnitsType)PlayerPrefs.GetInt(PREF_DISTANCE_UNITS, (int)DistanceUnitsType.Meters);
            s_AngleUnits = (AngleUnitsType)PlayerPrefs.GetInt(PREF_ANGLE_UNITS, (int)AngleUnitsType.Degrees);
            s_AngleChangeUnits = (AngleChangeUnitsType)PlayerPrefs.GetInt(PREF_ANGLE_CHANGE_UNITS, (int)AngleChangeUnitsType.Radians);
            s_SpeedUnits = (SpeedUnitsType)PlayerPrefs.GetInt(PREF_SPEED_UNITS, (int)SpeedUnitsType.MetersPerSecond);

            s_NodeGridSnapping = PlayerPrefs.GetInt(PREF_NODE_GRID_SNAPPING, 0) == 1;
            s_UIScale = PlayerPrefs.GetFloat(PREF_UI_SCALE, GetDefaultUIScale());
            s_ShowStats = PlayerPrefs.GetInt(PREF_SHOW_STATS, 0) == 1;
        }
    }
}
