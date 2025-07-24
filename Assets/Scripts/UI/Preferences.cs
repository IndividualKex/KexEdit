using UnityEngine;
using static KexEdit.UI.Constants;

namespace KexEdit.UI {
    public static class Preferences {
        private const string PREF_DISTANCE_UNITS = "DistanceUnits";
        private const string PREF_ANGLE_UNITS = "AngleUnits";
        private const string PREF_ANGLE_CHANGE_UNITS = "AngleChangeUnits";
        private const string PREF_SPEED_UNITS = "SpeedUnits";

        private const string PREF_NODE_GRID_SNAPPING = "NodeGridSnapping";
        private const string PREF_RIDE_CAMERA_HEIGHT = "RideCameraHeight";
        private const string PREF_UI_SCALE = "UIScale";
        private const string PREF_SHOW_STATS = "ShowStats";
        private const string PREF_SYNC_PLAYBACK = "SyncPlayback";
        private const string PREF_KEYFRAME_EDITOR = "KeyframeEditor";
        private const string PREF_SHOW_GIZMOS = "ShowGizmos";
        private const string PREF_AUTO_STYLE = "AutoStyle";
        private const string PREF_SKY_TYPE = "SkyType";

        private static DistanceUnitsType s_DistanceUnits;
        private static AngleUnitsType s_AngleUnits;
        private static AngleChangeUnitsType s_AngleChangeUnits;
        private static SpeedUnitsType s_SpeedUnits;

        private static SkyType s_SkyType;
        private static float s_RideCameraHeight;
        private static float s_UIScale;
        private static bool s_NodeGridSnapping;
        private static bool s_ShowStats;
        private static bool s_SyncPlayback;
        private static bool s_KeyframeEditor;
        private static bool s_ShowGizmos;
        private static bool s_AutoStyle;

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

        public static bool SyncPlayback {
            get => s_SyncPlayback;
            set {
                s_SyncPlayback = value;
                PlayerPrefs.SetInt(PREF_SYNC_PLAYBACK, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool KeyframeEditor {
            get => s_KeyframeEditor;
            set {
                s_KeyframeEditor = value;
                PlayerPrefs.SetInt(PREF_KEYFRAME_EDITOR, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool ShowGizmos {
            get => s_ShowGizmos;
            set {
                s_ShowGizmos = value;
                PlayerPrefs.SetInt(PREF_SHOW_GIZMOS, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool AutoStyle {
            get => s_AutoStyle;
            set {
                s_AutoStyle = value;
                PlayerPrefs.SetInt(PREF_AUTO_STYLE, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float RideCameraHeight {
            get => s_RideCameraHeight;
            set {
                s_RideCameraHeight = value;
                PlayerPrefs.SetFloat(PREF_RIDE_CAMERA_HEIGHT, value);
                PlayerPrefs.Save();
            }
        }

        public static SkyType SkyType {
            get => s_SkyType;
            set {
                s_SkyType = value;
                PlayerPrefs.SetInt(PREF_SKY_TYPE, (int)value);
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
            s_RideCameraHeight = PlayerPrefs.GetFloat(PREF_RIDE_CAMERA_HEIGHT, DEFAULT_RIDE_CAMERA_HEIGHT);
            s_UIScale = PlayerPrefs.GetFloat(PREF_UI_SCALE, GetDefaultUIScale());
            s_ShowStats = PlayerPrefs.GetInt(PREF_SHOW_STATS, 0) == 1;
            s_SyncPlayback = PlayerPrefs.GetInt(PREF_SYNC_PLAYBACK, 0) == 1;
            s_KeyframeEditor = PlayerPrefs.GetInt(PREF_KEYFRAME_EDITOR, 0) == 1;
            s_ShowGizmos = PlayerPrefs.GetInt(PREF_SHOW_GIZMOS, 0) == 1;
            s_AutoStyle = PlayerPrefs.GetInt(PREF_AUTO_STYLE, 0) == 1;
            s_SkyType = (SkyType)PlayerPrefs.GetInt(PREF_SKY_TYPE, (int)SkyType.Solid);
        }
    }
}
