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
        private const string PREF_SYNC_PLAYBACK = "SyncPlayback";
        private const string PREF_RECENT_FILES = "RecentFiles";

        private static DistanceUnitsType s_DistanceUnits;
        private static AngleUnitsType s_AngleUnits;
        private static AngleChangeUnitsType s_AngleChangeUnits;
        private static SpeedUnitsType s_SpeedUnits;

        private static string[] s_RecentFiles;
        private static float s_UIScale;
        private static bool s_NodeGridSnapping;
        private static bool s_ShowStats;
        private static bool s_SyncPlayback;

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

        public static string[] RecentFiles => s_RecentFiles ?? new string[0];

        public static void AddRecentFile(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return;

            var recentList = new System.Collections.Generic.List<string>(s_RecentFiles ?? new string[0]);

            recentList.Remove(filePath);
            recentList.Insert(0, filePath);

            if (recentList.Count > 5) {
                recentList.RemoveRange(5, recentList.Count - 5);
            }

            s_RecentFiles = recentList.ToArray();
            PlayerPrefs.SetString(PREF_RECENT_FILES, string.Join("|", s_RecentFiles));
            PlayerPrefs.Save();
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
            s_SyncPlayback = PlayerPrefs.GetInt(PREF_SYNC_PLAYBACK, 0) == 1;

            string recentFilesString = PlayerPrefs.GetString(PREF_RECENT_FILES, "");
            s_RecentFiles = string.IsNullOrEmpty(recentFilesString) ? new string[0] : recentFilesString.Split('|');
        }
    }
}
