using UnityEngine;

namespace KexEdit {
    public static class CartStylePreferences {
        private const string PREF_CURRENT_CART_STYLE = "CurrentCartStyle";

        private static string s_CurrentCartStyle;

        static CartStylePreferences() {
            LoadPreferences();
        }

        public static string CurrentCartStyle {
            get => s_CurrentCartStyle;
            set {
                s_CurrentCartStyle = value;
                PlayerPrefs.SetString(PREF_CURRENT_CART_STYLE, value);
                PlayerPrefs.Save();
            }
        }

        private static void LoadPreferences() {
            s_CurrentCartStyle = PlayerPrefs.GetString(PREF_CURRENT_CART_STYLE, "Default.json");
        }
    }
}
