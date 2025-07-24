using UnityEngine;

namespace KexEdit {
    public static class TrackStylePreferences {
        private const string PREF_CURRENT_TRACK_MESH = "CurrentTrackMesh";

        private static string s_CurrentTrackMesh;

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

        private static void LoadPreferences() {
            s_CurrentTrackMesh = PlayerPrefs.GetString(PREF_CURRENT_TRACK_MESH, "Default.json");
        }
    }
}
