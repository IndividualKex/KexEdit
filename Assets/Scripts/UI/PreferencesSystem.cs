using UnityEngine;

namespace KexEdit.UI {
    public static class PreferencesSystem {
        private const string PREF_NODE_GRID_SNAPPING = "NodeGridSnapping";
        private const string PREF_UI_SCALE = "UIScale";
        private const string PREF_SHOW_STATS = "ShowStats";

        private static float _uiScale = -1f;
        private static bool _nodeGridSnapping = false;
        private static bool _showStats = false;
        private static bool _initialized = false;

        public static float UIScale {
            get {
                if (!_initialized) LoadPreferences();
                return _uiScale;
            }
            set {
                _uiScale = value;
                PlayerPrefs.SetFloat(PREF_UI_SCALE, value);
                PlayerPrefs.Save();
            }
        }

        public static bool NodeGridSnapping {
            get {
                if (!_initialized) LoadPreferences();
                return _nodeGridSnapping;
            }
            set {
                _nodeGridSnapping = value;
                PlayerPrefs.SetInt(PREF_NODE_GRID_SNAPPING, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool ShowStats {
            get {
                if (!_initialized) LoadPreferences();
                return _showStats;
            }
            set {
                _showStats = value;
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
            _nodeGridSnapping = PlayerPrefs.GetInt(PREF_NODE_GRID_SNAPPING, 0) == 1;
            _uiScale = PlayerPrefs.GetFloat(PREF_UI_SCALE, GetDefaultUIScale());
            _showStats = PlayerPrefs.GetInt(PREF_SHOW_STATS, 0) == 1;
            _initialized = true;
        }
    }
}
