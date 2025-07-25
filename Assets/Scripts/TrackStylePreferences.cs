using UnityEngine;

namespace KexEdit {
    public static class TrackStylePreferences {
        private const string PREF_CURRENT_TRACK_MESH = "CurrentTrackMesh";
        private const string PREF_AUTO_STYLE = "AutoStyle";

        private static string s_CurrentTrackMesh;
        private static bool s_AutoStyle;

        static TrackStylePreferences() {
            LoadPreferences();
        }

        public static string CurrentTrackMesh {
            get => s_CurrentTrackMesh;
            set {
                s_CurrentTrackMesh = value;
                PlayerPrefs.SetString(PREF_CURRENT_TRACK_MESH, value);
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

        private static void LoadPreferences() {
            s_CurrentTrackMesh = PlayerPrefs.GetString(PREF_CURRENT_TRACK_MESH, "Default.json");
            s_AutoStyle = PlayerPrefs.GetInt(PREF_AUTO_STYLE, 0) == 1;
        }
    }
}
